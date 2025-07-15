using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using VRemoteClient.Models.Entities;

namespace VRemoteClient.Utils
{
    internal static class Capture
    {
        private static ConcurrentBag<Rectangle> changedBlocks = new ConcurrentBag<Rectangle>();
        //private static ConcurrentBag<ScreenBlock> blocks = new ConcurrentBag<ScreenBlock>();

        private static Bitmap? _previousFrame = null;
        private static readonly object _lockObject = new object();
        private static readonly object _lockObject2 = new object();
        static ImageCodecInfo encoder = ImageCodecInfo.GetImageEncoders()
                .First(c => c.FormatID == ImageFormat.Jpeg.Guid);

        internal static List<ScreenBlock> GetScreen()
        {
            EncoderParameters encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 75L);

            List<ScreenBlock> cells = new List<ScreenBlock>();
            lock (_lockObject)
            {
                using (Bitmap currentScreen = CaptureWindowsScreen1())
                {
                    if (_previousFrame == null)
                    {
                        // First capture - send full screen
                        byte[] compressedData = null;
                        using (var stream = new MemoryStream())
                        {
                            currentScreen.Save(stream, encoder, encoderParams);
                            compressedData = Utils.Extensions.Compress(stream.ToArray());
                        }
                        ScreenBlock cell = new ScreenBlock
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
                        using (Bitmap cur = currentScreen.Clone(new Rectangle(0, 0, currentScreen.Width, currentScreen.Height), PixelFormat.Format24bppRgb))
                        using (Bitmap pre = _previousFrame.Clone(new Rectangle(0, 0, _previousFrame.Width, _previousFrame.Height), PixelFormat.Format24bppRgb))
                        {
                            // Detect changes and create cells, 
                            dirtyRegions = DetectDirtyRegions(cur, pre);
                        }

                        if (dirtyRegions.Count > 0)
                        {
                            // Merge adjacent regions for efficiency
                            List<Rectangle> mergedRegions = MergeAdjacentRectangles(dirtyRegions);

                            //BitmapData bitmapData = currentScreen.LockBits(
                            //    new Rectangle(0, 0, currentScreen.Width, currentScreen.Height),
                            //    ImageLockMode.ReadOnly,
                            //    PixelFormat.Format24bppRgb
                            //);

                            //int stride = bitmapData.Stride;
                            //int bytes = Math.Abs(stride) * bitmapData.Height;
                            //byte[] screenBytes = new byte[bytes];

                            //Marshal.Copy(bitmapData.Scan0, screenBytes, 0, bytes);
                            //currentScreen.UnlockBits(bitmapData);


                            //Parallel.ForEach(mergedRegions, region =>
                            //{
                            //    using (Bitmap regionBitmap = CropFromBytes(screenBytes,bitmapData.Width, bitmapData.Height, stride, region))
                            //    {
                            //        byte[] compressedData;
                            //        using (var stream = new MemoryStream())
                            //        {
                            //            regionBitmap.Save(stream, encoder, encoderParams);
                            //            compressedData = Utils.Extensions.Compress(stream.ToArray());
                            //        }
                            //        blocks.Add(new ScreenBlock
                            //        {
                            //            IsFullScreen = false,
                            //            Rectangle = region,
                            //            Bytes = compressedData
                            //        });
                            //    }
                            //});
                            //cells.AddRange(blocks);
                            //lock (_lockObject)
                            //{
                            //    blocks = new ConcurrentBag<ScreenBlock>();
                            //}
                            for (int i = 0; i < mergedRegions.Count; i++)
                            {
                                using (Bitmap regionBitmap = CropBitmap(currentScreen, mergedRegions[i]))
                                {
                                    byte[] compressedData;
                                    using (var stream = new MemoryStream())
                                    {
                                        //string a = Path.Combine(Environment.CurrentDirectory, "images", "image_" + i + ".png");
                                        //Console.WriteLine(a);
                                        //regionBitmap.Save(a, ImageFormat.Png);
                                        regionBitmap.Save(stream, encoder, encoderParams);
                                        compressedData = Utils.Extensions.Compress(stream.ToArray());
                                    }
                                    ScreenBlock cell = new ScreenBlock
                                    {
                                        IsFullScreen = false,
                                        Rectangle = mergedRegions[i],
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
        //private static Bitmap CropFromBytes(byte[] screenBytes, int screenWidth, int screenHeight, int stride, Rectangle region)
        //{
        //    Bitmap cropped = new Bitmap(region.Width, region.Height, PixelFormat.Format24bppRgb);

        //    BitmapData targetData = cropped.LockBits(
        //        new Rectangle(0, 0, region.Width, region.Height),
        //        ImageLockMode.WriteOnly,
        //        PixelFormat.Format24bppRgb
        //    );

        //    int bytesPerPixel = 3;

        //    unsafe
        //    {
        //        fixed (byte* srcBase = screenBytes)
        //        {
        //            for (int y = 0; y < region.Height; y++)
        //            {
        //                byte* src = srcBase + ((region.Y + y) * stride) + (region.X * bytesPerPixel);
        //                byte* dst = (byte*)targetData.Scan0 + y * targetData.Stride;

        //                for (int x = 0; x < region.Width * bytesPerPixel; x++)
        //                {
        //                    dst[x] = src[x];
        //                }
        //            }
        //        }
        //    }

        //    cropped.UnlockBits(targetData);
        //    return cropped;
        //}
        internal static Bitmap CropBitmap(Bitmap source, Rectangle region)
        {
            // Validate region bounds
            region = Rectangle.Intersect(region, new Rectangle(0, 0, source.Width, source.Height));
            if (region.IsEmpty) return null;

            return source.Clone(region, source.PixelFormat);
        }
        internal static Bitmap CaptureWindowsScreen1()
        {
            var bounds = Screen.PrimaryScreen.Bounds;
            Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
            using (Graphics bitmapGraphics = Graphics.FromImage(bitmap))
            {
                IntPtr bitmapHdc = bitmapGraphics.GetHdc();
                IntPtr screenHdc = Libraries.GetDC(IntPtr.Zero);

                Libraries.BitBlt(bitmapHdc, 0, 0, bounds.Width, bounds.Height,
                       screenHdc, bounds.X, bounds.Y, 0x00CC0020); // SRCCOPY

                bitmapGraphics.ReleaseHdc(bitmapHdc);
                Libraries.ReleaseDC(IntPtr.Zero, screenHdc);
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
            const int blockSize = 8;

            // Parallel processing for better performance
            var regions = new List<Rectangle>();

            for (int y = 0; y < current.Height; y += blockSize)
            {
                for (int x = 0; x < current.Width; x += blockSize)
                {
                    int width = (current.Width - x) > blockSize ? blockSize : current.Width - x;
                    int height = (current.Height - y) > blockSize ? blockSize : current.Height - y;

                    Rectangle block = new Rectangle(x, y,
                        width,
                        height);
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

                Parallel.ForEach(regions,
                    new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
                    block =>
                    {
                        if (IsBlockChanged(currentData, previousData, block))
                            changedBlocks.Add(block);
                    });
                var result = changedBlocks.ToList() ;

                return result;
            }
            finally
            {
                if (currentData != null)
                {
                    current.UnlockBits(currentData);
                }
                if (previousData != null)
                {
                    previous.UnlockBits(previousData);
                }
                lock (_lockObject2)
                {
                    changedBlocks = new ConcurrentBag<Rectangle>();
                }
            }
        }

        private static List<Rectangle> MergeAdjacentRectangles(List<Rectangle> rectangles)
        {
            if (rectangles.Count <= 1) return rectangles;

            var merged = new List<Rectangle>();
            var sorted = rectangles.OrderBy(r => r.Y).ThenBy(r => r.X).ToList();

            for(int j= 0; j< sorted.Count; j++)
            {
                bool wasMerged = false;

                for (int i = 0; i < merged.Count; i++)
                {
                    if (CanMerge(merged[i], sorted[j]))
                    {
                        merged[i] = Rectangle.Union(merged[i], sorted[j]);
                        wasMerged = true;
                        break;
                    }
                }

                if (!wasMerged)
                {
                    merged.Add(sorted[j]);
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
        private static int AbsBitwise(int x) => (x + (x >> 31)) ^ (x >> 31);

        private unsafe static bool IsBlockChanged(BitmapData currentData, BitmapData previousData, Rectangle block)
        {
            byte* currentPtr = (byte*)currentData.Scan0;
            byte* previousPtr = (byte*)previousData.Scan0;
            int stride = currentData.Stride;
            const int threshold = 10;

            // move pointer to start of the block
            currentPtr += block.Y * stride + block.X * 3;
            previousPtr += block.Y * stride + block.X * 3;

            int blockWidth = block.Width;
            int blockHeight = block.Height;

            for (int y = 0; y < blockHeight; y++)
            {
                int rowStride = y * stride;
                byte* currentRow = currentPtr + rowStride;
                byte* previousRow = previousPtr + rowStride;

                for (int x = 0; x < blockWidth; x++)
                {
                    int index = x * 3;
                    // compare the RGB values of the current and previous frames
                    int bDiff = currentRow[index] - previousRow[index];
                    int gDiff = currentRow[index + 1] - previousRow[index + 1];
                    int rDiff = currentRow[index + 2] - previousRow[index + 2];

                    if (AbsBitwise(bDiff) > threshold ||
                        AbsBitwise(gDiff) > threshold ||
                        AbsBitwise(rDiff) > threshold)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
       /* [Obsolete("Not Use")]
        private unsafe static bool IsBlockChanged(BitmapData currentData, BitmapData previousData, Rectangle block)
        {
            byte* currentPtr = (byte*)currentData.Scan0;
            byte* previousPtr = (byte*)previousData.Scan0;

            int stride = currentData.Stride;
            const int threshold = 10; // Noise threshold

            for (int y = 0; y < block.Height; y++)
            {
                int rowOffset = (block.Y + y) * stride + block.X * 3;

                for (int x = 0; x < block.Width; x++)
                {
                    // CRITICAL FIX: Add block's position to get actual pixel coordinates
                    int actualY = block.Y + y;
                    int actualX = block.X + x;
                    int offset = actualY * stride + actualX * 3;

                    int bDiff = currentPtr[offset] - previousPtr[offset]; //B in RGB
                    int gDiff = currentPtr[offset + 1] - previousPtr[offset + 1]; //G in RGB
                    int rDiff = currentPtr[offset + 2] - previousPtr[offset + 2]; //R in RGB

                    // Check with threshold to avoid false positives from noise
                    if (((bDiff + (bDiff >> 31)) ^ (bDiff >> 31)) > threshold ||
                        ((gDiff + (gDiff >> 31)) ^ (gDiff >> 31)) > threshold ||
                        ((rDiff + (rDiff >> 31)) ^ (rDiff >> 31)) > threshold)
                    {
                        return true;
                    }
                }
            }

            return false;
        }*/

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
