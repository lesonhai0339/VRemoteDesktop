using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Threading.Timer;

namespace RemoteClient.Remote
{
    public partial class TestScreenChange : Form
    {
        private bool flag = false;
        private Timer _timer;
        public TestScreenChange()
        {
            InitializeComponent();
            BackColor = Color.White;
            textBox1.BorderStyle = BorderStyle.None;
            _timer = new Timer(Capture, null, 0, (1000 / 15));
        }
        private void Capture(object state)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            var screenStateLogger = new ScreenStateLogger();
            screenStateLogger.ScreenRefreshed += (sender, data) =>
            {
                //New frame in data
                Console.WriteLine(data);
            };
            screenStateLogger.Start();
            //var bound = Screen.PrimaryScreen.Bounds;
            //Bitmap bitmap = new Bitmap(bound.Width, bound.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            //Graphics g = Graphics.FromImage(bitmap);
            //g.CopyFromScreen(bound.X, bound.Y, 0, 0, new Size(10, 10), CopyPixelOperation.SourceCopy);
            stopwatch.Stop();
            Console.WriteLine($"Eslaped time: {stopwatch.Elapsed.TotalMilliseconds}");
            return;
            var a = CaptureScreen.GetScreen();
            foreach (var i in a)
            {
                Console.WriteLine($"Change: {i.TotalSize} bytes");
            }
            Console.WriteLine("----------------------------------------\n\n");
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
        }
    }
}
