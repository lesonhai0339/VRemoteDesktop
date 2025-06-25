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
            var a = CaptureScreen.GetScreen();
            stopwatch.Stop();
            foreach (var i in a)
            {
                Console.WriteLine($"Change: {i.TotalSize} bytes");
            }
            Console.WriteLine($"Eslaped time: {stopwatch.Elapsed.TotalMilliseconds}");
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
