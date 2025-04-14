using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Client
{
    public partial class Form1 : Form
    {
        private TCPClient _tcpClient;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            _tcpClient = new TCPClient();
            Connect();
        }
        public virtual TCPClient Client
        {
            get => _tcpClient;
            set
            {
                if (_tcpClient != null)
                {
                    _tcpClient.ImageReceived -= Callback;
                }
                _tcpClient = value;
                if (_tcpClient != null)
                {
                    _tcpClient.ImageReceived += Callback;
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
        private void Callback(object sender, ImageEventArgs e)
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
