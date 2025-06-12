using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using FFMpegCore.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using FFMpegCore;

namespace RemoteClient
{
    public class WindowsScreen
    {
        public class ScreenCaptureSource
        {
            public bool isCapture;
            private MemoryStream _currentFrame;
            public ScreenCaptureSource()
            {
                _currentFrame = new MemoryStream();
            }
            public string GetStreamArguments()
            {
                //return $"-f rawvideo -pixel_format bgr24 -video_size {_options.CaptureWidth}x{_options.CaptureHeight} -framerate {_options.FrameRate}";
                return $"-f rawvideo -pixel_format bgr24 -video_size 1920x1080 -framerate 24";
            }

            public async Task WriteAync(Stream outputStream, CancellationToken cancellation = default)
            {
                isCapture = true;
                var frame = TimeSpan.FromMilliseconds(1000.0 /24); // 24 FPS
                try
                {
                    while(isCapture  && cancellation.IsCancellationRequested)
                    {
                        var frameStart = DateTime.Now;

                        var bitmap = CaptureScreen();
                        var byteData = BitmapTopByteArray(bitmap);

                        await outputStream.WriteAsync(byteData, 0, byteData.Length, cancellation);
                        await outputStream.FlushAsync(cancellation);

                        var eslapsed = DateTime.Now - frameStart;
                        var waitTime = frame - eslapsed;
                        if(waitTime > TimeSpan.Zero)
                        {
                            await Task.Delay(waitTime, cancellation);
                        }

                    }
                }
                catch
                {

                }
            }
            public Bitmap CaptureScreen()
            {
                Rectangle bounds = new Rectangle(0, 0, Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
                Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height);
                Graphics graphics = Graphics.FromImage(bitmap);
                graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

                return bitmap;
            }
            public byte[] BitmapTopByteArray(Bitmap bitmap)
            {
                var data = bitmap.LockBits(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format24bppRgb);

                var byteData = new byte[Math.Abs(data.Stride) * bitmap.Height];
                Marshal.Copy(data.Scan0, byteData, 0, byteData.Length);
                bitmap.UnlockBits(data);
                return byteData;
            }
            public void Dispose()
            {
                isCapture = false;
                _currentFrame?.Dispose();
            }
        }
        public byte[] GrabDesktop()
        {
            Rectangle bound = Screen.PrimaryScreen.Bounds;
            Bitmap screenshot = new Bitmap(bound.Width, bound.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            Graphics graphics = Graphics.FromImage(screenshot);
            graphics.CopyFromScreen(bound.X, bound.Y, 0, 0, bound.Size, CopyPixelOperation.SourceCopy);


            using (MemoryStream stream = new MemoryStream())
            {
                screenshot.Save(stream, ImageFormat.Jpeg);
                return stream.ToArray();
            }
        }
    }
}
