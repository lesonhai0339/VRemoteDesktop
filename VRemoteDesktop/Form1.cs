using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace VRemoteDesktop
{
    public partial class Form1 : Form
    {
        private Class2 _class2;
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Client = new Class2();
            Connect();
        }
        public virtual Class2 Client
        {
            get => _class2;
            set
            {
                if (_class2 != null)
                {
                    _class2.ImageReceived -= Callback;
                    _class2.TextReceived -= TextCallback;
                }
                _class2 = value;
                if (_class2 != null)
                {
                    _class2.ImageReceived += Callback;
                    _class2.TextReceived += TextCallback;
                }
            }
        }
        private void Connect()
        {
            string remoteHostName = "27.0.12.78";
            int remotePort = 2399;
            var address = IPAddress.Parse(remoteHostName);
            IPEndPoint remoteEP = new IPEndPoint(address, remotePort);

            Client.Connect(remoteEP);     
        }
        public void TextCallback(object sender, TextEventArgs e)
        {
            if (textBox1.InvokeRequired)
            {
                textBox1.Invoke(new Action(() =>
                {
                    textBox1.Text = e.Data;
                }));
            }
            else
            {
                textBox1.Text = e.Data;
            }
        }
        private void Callback(object sender , ImageEventArgs e)
        {
            Console.WriteLine("Callback Called");

            if (pictureBox1.InvokeRequired)
            {
                pictureBox1.Invoke(new Action(() =>
                {
                    using (MemoryStream stream = new MemoryStream(e.Data))
                    {
                        pictureBox1.Image = Image.FromStream(stream);
                    }
                }));
            }
            else
            {
                using (MemoryStream stream = new MemoryStream(e.Data))
                {
                    pictureBox1.Image = Image.FromStream(stream);
                }
            }
        }
    }
}
