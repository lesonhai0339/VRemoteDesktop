using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Threading.Timer;

namespace RemoteClient.Remote
{
    public partial class TestScreenChange : Form
    {
        [DllImport("gdi32.dll")]
        static extern bool BitBlt(IntPtr hdc, int nXDest, int nYDest, int nWidth, int nHeight,
    IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);
        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        ShowImage showImage;

        private bool flag = false;
        private bool flag1 = false;
        private Timer _timer;
        private VCaptureScreen _vCaptureScreen;
        public TestScreenChange()
        {
            InitializeComponent();
            BackColor = Color.White;
            textBox1.BorderStyle = BorderStyle.None;
            //_timer = new Timer(Capture, null, 0, (1000 / 15));
            _vCaptureScreen = new VCaptureScreen();
        }
        private void test1()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();
                    var bounds = Screen.PrimaryScreen.Bounds;
                    using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppRgb))
                    {
                        using (Graphics bitmapGraphics = Graphics.FromImage(bitmap))
                        {
                            IntPtr bitmapHdc = bitmapGraphics.GetHdc();
                            IntPtr screenHdc = GetDC(IntPtr.Zero);

                            BitBlt(bitmapHdc, 0, 0, bounds.Width, bounds.Height,
                                   screenHdc, bounds.X, bounds.Y, 0x00CC0020); // SRCCOPY

                            bitmapGraphics.ReleaseHdc(bitmapHdc);
                            ReleaseDC(IntPtr.Zero, screenHdc);
                        }
                    }
                    stopwatch.Stop();
                    Console.WriteLine($"Eslaped time: {stopwatch.Elapsed.TotalMilliseconds}");
                }
            });
        }
        private void test2()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();
                    var bound = Screen.PrimaryScreen.Bounds;
                    using (Bitmap bitmap = new Bitmap(bound.Width, bound.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb))
                    {
                        Graphics g = Graphics.FromImage(bitmap);
                        g.CopyFromScreen(bound.X, bound.Y, 0, 0, bound.Size, CopyPixelOperation.SourceCopy);
                    }
                    stopwatch.Stop();
                    Console.WriteLine($"Eslaped time: {stopwatch.Elapsed.TotalMilliseconds}");
                }
            });
        }
        private void test3()
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            var screenStateLogger = new ScreenStateLogger();
            screenStateLogger.ScreenRefreshed += (sender, data) =>
            {
                //New frame in data
                Console.WriteLine($"Call {data.Length} - {DateTime.Now.ToString("hh:mm:ss tt")}");
            };
            screenStateLogger.Start();
            stopwatch.Stop();
            Console.WriteLine($"Eslaped time: {stopwatch.Elapsed.TotalMilliseconds}");
        }
        private void test4()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    var a = CaptureScreen.GetScreen();
                }
            });
        }
        private void Capture(object state)
        {
            test4();
            //Stopwatch stopwatch = new Stopwatch();
            //stopwatch.Start();
            //var screenStateLogger = new ScreenStateLogger();
            //screenStateLogger.ScreenRefreshed += (sender, data) =>
            //{
            //    //New frame in data
            //    Console.WriteLine(data);
            //};
            //screenStateLogger.Start();
            ////var bound = Screen.PrimaryScreen.Bounds;
            ////Bitmap bitmap = new Bitmap(bound.Width, bound.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            ////Graphics g = Graphics.FromImage(bitmap);
            ////g.CopyFromScreen(bound.X, bound.Y, 0, 0, new Size(10, 10), CopyPixelOperation.SourceCopy);
            //stopwatch.Stop();
            //Console.WriteLine($"Eslaped time: {stopwatch.Elapsed.TotalMilliseconds}");
            //return;
            //var a = CaptureScreen.GetScreen();
            //foreach (var i in a)
            //{
            //    Console.WriteLine($"Change: {i.TotalSize} bytes");
            //}
            //Console.WriteLine("----------------------------------------\n\n");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (!flag)
            {
                flag = true;
                textBox1.Text = "OK";
                textBox1.ForeColor = Color.Red;
            }
            else
            {
                flag = false;
                textBox1.Text = "";
            }
            ShowImage showForm = new ShowImage(null);
            showForm.Show();
            //Capture(null);
            //return;
           // _vCaptureScreen.Test();

            //Stopwatch stopwath = new Stopwatch();
            //stopwath.Start();
            //var bounds = Screen.PrimaryScreen.Bounds;
            //Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
            //using (Graphics g = Graphics.FromImage(bitmap))
            //{
            //    g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            //}


            ////var a = _vCaptureScreen.SplitToRegions(bitmap);
            ////List<Bitmap> crops = a.Select(x => _vCaptureScreen.Crop(bitmap, x)).ToList();
            ////var mergeBitmap = MergeRegions(crops, a, bitmap.Size);
            //if (!flag1)
            //{
            //    flag1 = true;
            //    showImage = new ShowImage(bitmap);
            //    showImage.Show();
            //}
            //stopwath.Stop();
            //Console.WriteLine($"Eslaped time: {stopwath.Elapsed.TotalMilliseconds}");
        }

        public Bitmap MergeRegions(List<Bitmap> croppedBitmaps, List<Rectangle> regions, Size originalSize)
        {
            Bitmap result = new Bitmap(originalSize.Width, originalSize.Height);

            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.Clear(Color.Transparent); // or Color.White if you prefer

                for (int i = 0; i < croppedBitmaps.Count && i < regions.Count; i++)
                {
                    graphics.DrawImageUnscaled(croppedBitmaps[i], regions[i].Location);
                }
            }

            return result;
        }
        private void TestScreenChange_Load(object sender, EventArgs e)
        {

        }
    }
}
