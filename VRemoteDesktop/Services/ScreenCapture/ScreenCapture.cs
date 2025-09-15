using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static VRemoteDesktop.Interop.Win32Apis;
using VRemoteDesktop.Models;
using System.Diagnostics;
using static VRemoteDesktop.Utils.DefaultScreen;

namespace VRemoteDesktop.Services.ScreenCapture
{
    public interface IScreenCapture
    {
        List<ScreenRegion> GetCurrentScreen();
        List<ScreenRegion> GetScreen();
        void Renew();
        void Dispose();
    }
    public class ScreenCapture : IScreenCapture, IDisposable
    {
        private int BLOCK_SIZE = DEFAULT_BLOCK_SIZE; // Size of each block for change detection
        private bool _isDisposed = false;
        private ConcurrentBag<Rectangle> changedBlocks = new ConcurrentBag<Rectangle>();

        private Bitmap _previousFrame;
        private object _lock;
        private object _lockObject;
        private object _lockObject2;
        private ImageCodecInfo encoder;
        private EncoderParameters encoderParams;
        public ScreenCapture()
        {
            _previousFrame = null;
            _lock = new object();
            _lockObject = new object();
            _lockObject2 = new object();
            encoder = ImageCodecInfo.GetImageEncoders()
                .First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 50L);
        }
        public void Renew()
        {
            lock (_lock)
            {
                _previousFrame = null;
            }
        }
        public List<ScreenRegion> GetCurrentScreen()
        {
            lock (_lock)
            {
                using (Bitmap currentScreen = CaptureWindowsScreen1())
                {
                    _previousFrame = currentScreen.Clone(
                          new Rectangle(0, 0, currentScreen.Width, currentScreen.Height),
                          PixelFormat.Format24bppRgb
                       );
                    return FullScreenRegion(currentScreen);
                }
            }
        }
        public List<ScreenRegion> GetScreen()
        {
            List<ScreenRegion> regions = new List<ScreenRegion>();
            lock (_lockObject)
            {
                using (Bitmap currentScreen = CaptureWindowsScreen1())
                {
                    if (_previousFrame == null)
                    {
                        _previousFrame = currentScreen.Clone(
                           new Rectangle(0, 0, currentScreen.Width, currentScreen.Height),
                           PixelFormat.Format24bppRgb
                        );
                        return FullScreenRegion(currentScreen);
                    }
                    List<Rectangle> dirtyRegions = new List<Rectangle>();
                    BitmapData cur = null, pre = null;

                    try
                    {
                        cur = currentScreen.LockBits(new Rectangle(0, 0, currentScreen.Width, currentScreen.Height),
                            ImageLockMode.ReadOnly,
                            PixelFormat.Format24bppRgb);
                        pre = _previousFrame.LockBits(new Rectangle(0, 0, _previousFrame.Width, _previousFrame.Height),
                            ImageLockMode.ReadOnly,
                            PixelFormat.Format24bppRgb);

                        dirtyRegions = DetectDirtyRegions(cur, pre);
                    }
                    finally
                    {
                        if (cur != null)
                            currentScreen.UnlockBits(cur);
                        if (pre != null)
                            _previousFrame.UnlockBits(pre);
                    }

                    using (Graphics g = Graphics.FromImage(_previousFrame))
                    {
                        g.DrawImageUnscaled(currentScreen, 0, 0);
                    }
                    return MakeScreenRegions(currentScreen, dirtyRegions);
                }
            }
        }
        private List<ScreenRegion> FullScreenRegion(Bitmap fullScreen)
        {
            using (var stream = new MemoryStream())
            {
                fullScreen.Save(stream, encoder, encoderParams);
                ScreenRegion region = new ScreenRegion
                {
                    IsFullScreen = true,
                    Rectangle = new Rectangle(0, 0, fullScreen.Width, fullScreen.Height),
                    Bytes = stream.ToArray()
                };
                return new List<ScreenRegion> { region };
            }
        }
        private List<ScreenRegion> MakeScreenRegions(Bitmap currentScreen, List<Rectangle> dirtyRegions)
        {
            List<ScreenRegion> regions = new List<ScreenRegion>();
            if(dirtyRegions.Count == 0)
                return new List<ScreenRegion>();

            // Merge adjacent regions for efficiency
            List<Rectangle> mergedRegions = MergeAdjacentRectangles(dirtyRegions);

            for (int i = 0; i < mergedRegions.Count; i++)
            {
                using (Bitmap regionBitmap = CropBitmap(currentScreen, mergedRegions[i]))
                {
                    using (var stream = new MemoryStream())
                    {
                        regionBitmap.Save(stream, encoder, encoderParams);
                        ScreenRegion region = new ScreenRegion
                        {
                            IsFullScreen = false,
                            Rectangle = mergedRegions[i],
                            Bytes = stream.ToArray()
                        };
                        regions.Add(region);
                    }
                }
            }
            return regions; 
        }
        private Bitmap CropBitmap(Bitmap source, Rectangle region)
        {
            // Validate region bounds
            region = Rectangle.Intersect(region, new Rectangle(0, 0, source.Width, source.Height));
            if (region.IsEmpty) return null;

            return source.Clone(region, source.PixelFormat);
        }
        private Bitmap CaptureWindowsScreen1()
        {
            var bounds = Screen.PrimaryScreen.Bounds;
            Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
            using (Graphics bitmapGraphics = Graphics.FromImage(bitmap))
            {
                IntPtr bitmapHdc = bitmapGraphics.GetHdc();
                IntPtr screenHdc = CaptureApis.GetDC(IntPtr.Zero);

                CaptureApis.BitBlt(bitmapHdc, 0, 0, bounds.Width, bounds.Height,
                       screenHdc, bounds.X, bounds.Y, 0x00CC0020); // SRCCOPY

                bitmapGraphics.ReleaseHdc(bitmapHdc);
                CaptureApis.ReleaseDC(IntPtr.Zero, screenHdc);
            }
            return bitmap;
        }
        private Bitmap CaptureWindowsScreen()
        {
            Rectangle bounds = Screen.PrimaryScreen.Bounds;
            Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);

            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            }

            return bitmap;
        }
        private List<Rectangle> GenerateRegions(BitmapData curBitmap, BitmapData preBitmap)
        {
            var regions = new List<Rectangle>();

            for (int y = 0; y < curBitmap.Height; y += BLOCK_SIZE)
            {
                for (int x = 0; x < curBitmap.Width; x += BLOCK_SIZE)
                {
                    int width = curBitmap.Width - x > BLOCK_SIZE ? BLOCK_SIZE : preBitmap.Width - x;
                    int height = curBitmap.Height - y > BLOCK_SIZE ? BLOCK_SIZE : preBitmap.Height - y;
                    Rectangle block = new Rectangle(x, y,
                        width,
                        height);
                    regions.Add(block);
                }
            }
            return regions;
        }
        private List<Rectangle> DetectDirtyRegions(BitmapData curBitmap, BitmapData preBitmap)
        {
            var regions = GenerateRegions(curBitmap, preBitmap);
            var maxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2);

            try
            {
                Parallel.ForEach(regions,
                    new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
                    block =>
                    {
                        if (IsBlockChanged(curBitmap, preBitmap, block))
                            changedBlocks.Add(block);
                    });
                var result = changedBlocks.ToList();
                return result;
            }
            finally
            {
                lock (_lockObject2)
                {
                    changedBlocks = new ConcurrentBag<Rectangle>();
                }
            }
        }
        private List<Rectangle> MergeAdjacentRectangles(List<Rectangle> rectangles)
        {
            if (rectangles.Count <= 1) return rectangles;

            var merged = new List<Rectangle>();
            var sorted = rectangles.OrderBy(r => r.Y).ThenBy(r => r.X).ToList();

            for (int j = 0; j < sorted.Count; j++)
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
        private bool CanMerge(Rectangle rect1, Rectangle rect2)
        {
            // Check if rectangles are adjacent or overlapping
            Rectangle union = Rectangle.Union(rect1, rect2);
            int unionArea = union.Width * union.Height;
            int combinedArea = rect1.Width * rect1.Height + rect2.Width * rect2.Height;

            // Only merge if efficiency is above threshold (avoid creating large empty areas)
            double efficiency = (double)combinedArea / unionArea;
            return efficiency > 0.75; // 75% efficiency threshold
        }
        //this method same with Math.abs()
        private int AbsBitwise(int x) => x + (x >> 31) ^ x >> 31;
        private unsafe bool IsBlockChanged(BitmapData currentData, BitmapData previousData, Rectangle block)
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
        // Cleanup method
        ~ScreenCapture()
        {
            Dispose(false);
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_isDisposed) return;
                lock (_lockObject)
                {
                    _previousFrame?.Dispose();
                    _previousFrame = null;

                    while (changedBlocks.TryTake(out _)) { }
                }
                _isDisposed = true;
            }
        }
    }
}
