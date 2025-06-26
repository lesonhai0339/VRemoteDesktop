using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
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


        private bool flag = false;
        private Timer _timer;
        public TestScreenChange()
        {
            InitializeComponent();
            BackColor = Color.White;
            textBox1.BorderStyle = BorderStyle.None;
            //_timer = new Timer(Capture, null, 0, (1000 / 15));
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
                    using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb))
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
        private void Capture(object state)
        {
            test3();
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
            Capture(null);
        }

        private void TestScreenChange_Load(object sender, EventArgs e)
        {

        }
    }
}
