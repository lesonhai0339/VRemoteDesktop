using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Imaging;

namespace RemoteClient.Remote
{
    public class CaptureCell : IDisposable
    {
        public bool IsFullScreen { get; set; } // Indicates if this cell is a full-screen capture
        public Rectangle Rectangle { get; set; }
        public byte[] Bytes { get; set; }
        public int TotalSize => Bytes?.Length ?? 0; // Total size of the captured bytes

        public void Dispose()
        {
            Bytes = null;
        }
    }

    internal static class CaptureScreen
    {
        private static Bitmap _previousFrame;
        private static readonly object _lockObject = new object();
        [DllImport("gdi32.dll")]
        static extern bool BitBlt(IntPtr hdc, int nXDest, int nYDest, int nWidth, int nHeight,IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);
        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")]

        static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        internal static List<CaptureCell> GetScreen()
        {
            List<CaptureCell> cells = new List<CaptureCell>();

            lock (_lockObject)
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                using (Bitmap currentScreen = CaptureWindowsScreen1())
                {
                    stopwatch.Stop();
                    Console.WriteLine($"Capture time: {stopwatch.Elapsed.TotalMilliseconds}");
                    if (_previousFrame == null)
                    {

                        // First capture - send full screen
                        byte[] compressedData = null;
                        using (var stream = new MemoryStream())
                        {
                            currentScreen.Save(stream, ImageFormat.Jpeg);
                            compressedData = stream.ToArray();
                        }
                        CaptureCell cell = new CaptureCell
                        {
                            IsFullScreen = true,
                            Rectangle = new Rectangle(0, 0, currentScreen.Width, currentScreen.Height),
                            Bytes = compressedData
                        };
                        cells.Add(cell);

                        // Store current frame as previous
                        _previousFrame = currentScreen.Clone(
                            new Rectangle(0, 0, currentScreen.Width, currentScreen.Height),
                            PixelFormat.Format24bppRgb
                        );
                    }
                    else
                    {
                        List<Rectangle> dirtyRegions = new List<Rectangle>();
                        using (Bitmap cur = currentScreen.Clone() as Bitmap)
                        using (Bitmap pre = _previousFrame.Clone() as Bitmap)
                        {
                            // Detect changes and create cells, 
                            dirtyRegions = DetectDirtyRegions(cur, pre);
                        }


                        if (dirtyRegions.Count > 0)
                        {
                            // Merge adjacent regions for efficiency
                            List<Rectangle> mergedRegions = MergeAdjacentRectangles(dirtyRegions);

                            foreach (var region in mergedRegions)
                            {
                                using (Bitmap regionBitmap = CropBitmap(currentScreen, region))
                                {
                                    byte[] compressedData = null;
                                    using (var stream = new MemoryStream())
                                    {
                                        regionBitmap.Save(stream , ImageFormat.Jpeg);
                                        compressedData = stream.ToArray();
                                    }
                                    CaptureCell cell = new CaptureCell
                                    {
                                        IsFullScreen = false,
                                        Rectangle = region,
                                        Bytes = compressedData
                                    };
                                    cells.Add(cell);
                                }
                            }

                            // Update previous frame
                            _previousFrame?.Dispose();
                            _previousFrame = currentScreen.Clone(
                                new Rectangle(0, 0, currentScreen.Width, currentScreen.Height),
                                PixelFormat.Format24bppRgb
                            );
                        }
                        // If no changes, return empty list
                    }
                }
            }

            return cells;
        }

        internal static Bitmap CropBitmap(Bitmap source, Rectangle region)
        {
            // Validate region bounds
            region = Rectangle.Intersect(region, new Rectangle(0, 0, source.Width, source.Height));
            if (region.IsEmpty) return null;

            Bitmap croppedBitmap = new Bitmap(region.Width, region.Height, source.PixelFormat);

            using (Graphics g = Graphics.FromImage(croppedBitmap))
            {
                g.DrawImage(source,
                    new Rectangle(0, 0, region.Width, region.Height),
                    region,
                    GraphicsUnit.Pixel);
            }

            return croppedBitmap;
        }
        internal static Bitmap CaptureWindowsScreen1()
        {
            var bounds = Screen.PrimaryScreen.Bounds;
            Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
            using (Graphics bitmapGraphics = Graphics.FromImage(bitmap))
            {
                IntPtr bitmapHdc = bitmapGraphics.GetHdc();
                IntPtr screenHdc = GetDC(IntPtr.Zero);

                BitBlt(bitmapHdc, 0, 0, bounds.Width, bounds.Height,
                       screenHdc, bounds.X, bounds.Y, 0x00CC0020); // SRCCOPY

                bitmapGraphics.ReleaseHdc(bitmapHdc);
                ReleaseDC(IntPtr.Zero, screenHdc);
            }
            return bitmap;
        }
        internal static Bitmap CaptureWindowsScreen()
        {
            Rectangle bounds = Screen.PrimaryScreen.Bounds;
            Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);

            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            }

            return bitmap;
        }

        internal static List<Rectangle> DetectDirtyRegions(Bitmap current, Bitmap previous)
        {
            var dirtyRegions = new List<Rectangle>();
            const int blockSize = 16;

            // Parallel processing for better performance
            var regions = new List<Rectangle>();

            for (int y = 0; y < current.Height; y += blockSize)
            {
                for (int x = 0; x < current.Width; x += blockSize)
                {
                    Rectangle block = new Rectangle(x, y,
                        Math.Min(blockSize, current.Width - x),
                        Math.Min(blockSize, current.Height - y));
                    regions.Add(block);
                }
            }
            BitmapData currentData = null;
            BitmapData previousData = null;
            var maxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2);

            try
            {
                currentData = current.LockBits(new Rectangle(0, 0, current.Width, current.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                previousData = previous.LockBits(new Rectangle(0, 0, previous.Width, previous.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

                // Check blocks in parallel
                var changedBlocks = regions.AsParallel().WithDegreeOfParallelism(maxDegreeOfParallelism).WithExecutionMode(ParallelExecutionMode.ForceParallelism).Where(block => IsBlockChanged(currentData, previousData, block)).ToList();
                //var changedBlocks = regions.Where(block => IsBlockChanged(currentData, previousData, block)).ToList();

                return changedBlocks;
            }
            finally
            {
                if(currentData != null)
                {
                    current.UnlockBits(currentData);
                }
                if(previousData != null)
                {
                    previous.UnlockBits(previousData);
                }
            }
        }

        private static List<Rectangle> MergeAdjacentRectangles(List<Rectangle> rectangles)
        {
            if (rectangles.Count <= 1) return rectangles;

            var merged = new List<Rectangle>();
            var sorted = rectangles.OrderBy(r => r.Y).ThenBy(r => r.X).ToList();

            foreach (var rect in sorted)
            {
                bool wasMerged = false;

                for (int i = 0; i < merged.Count; i++)
                {
                    if (CanMerge(merged[i], rect))
                    {
                        merged[i] = Rectangle.Union(merged[i], rect);
                        wasMerged = true;
                        break;
                    }
                }

                if (!wasMerged)
                {
                    merged.Add(rect);
                }
            }

            return merged;
        }

        private static bool CanMerge(Rectangle rect1, Rectangle rect2)
        {
            // Check if rectangles are adjacent or overlapping
            Rectangle union = Rectangle.Union(rect1, rect2);
            int unionArea = union.Width * union.Height;
            int combinedArea = (rect1.Width * rect1.Height) + (rect2.Width * rect2.Height);

            // Only merge if efficiency is above threshold (avoid creating large empty areas)
            double efficiency = (double)combinedArea / unionArea;
            return efficiency > 0.75; // 75% efficiency threshold
        }

        private unsafe static bool IsBlockChanged(BitmapData current, BitmapData previous, Rectangle block)
        {
            BitmapData currentData = null;
            BitmapData previousData = null;

            try
            {
                previousData = previous;
                currentData = current;
                byte* currentPtr = (byte*)currentData.Scan0;
                byte* previousPtr = (byte*)previousData.Scan0;

                int stride = currentData.Stride;
                int bytesPerPixel = 3;
                const int threshold = 10; // Noise threshold

                for (int y = 0; y < block.Height; y++)
                {
                    for (int x = 0; x < block.Width; x++)
                    {
                        // CRITICAL FIX: Add block's position to get actual pixel coordinates
                        int actualY = block.Y + y;
                        int actualX = block.X + x;
                        int offset = actualY * stride + actualX * bytesPerPixel;

                        // Check with threshold to avoid false positives from noise
                        if (Math.Abs(currentPtr[offset] - previousPtr[offset]) > threshold ||
                            Math.Abs(currentPtr[offset + 1] - previousPtr[offset + 1]) > threshold ||
                            Math.Abs(currentPtr[offset + 2] - previousPtr[offset + 2]) > threshold)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
            finally
            {
            }
        }

        // Cleanup method
        internal static void Dispose()
        {
            lock (_lockObject)
            {
                _previousFrame?.Dispose();
                _previousFrame = null;
            }
        }
    }
}