using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Client
{
    public partial class Form1 : Form
    {
        private TCPClient _tcpClient;
        private KeyboardHook _keyboardHook;
        private KeyboardSendEventHandler _keyboardSendEventHandler;
        private KeyboardReceivedEventHandler _keyboardReceivedEventHandler;
        private KeyboardSimulator _keyboardSimulator;

        public Form1()
        {
            InitializeComponent();
            _keyboardHook = new KeyboardHook();
            _keyboardSimulator = new KeyboardSimulator();
            _keyboardSendEventHandler = new KeyboardSendEventHandler();
            _keyboardReceivedEventHandler = new KeyboardReceivedEventHandler();
            _keyboardHook.KeyPressed += Form1_KeyDown;

        }
        private void Form1_KeyDown(object sender, KeyMessageEventArgs e)
        {
            bool flag;
            label1.Text = $"{e.KeyCode} - {e.KeyType}";
            byte[] byteKey = _keyboardSendEventHandler.KeyBuilder(e);
            Keys keyReceived = _keyboardReceivedEventHandler.KeyboardReceived(byteKey);



            byte[] byteSend = new byte[1024];
            byte type = 0x01;
            byte isHost = 0x01;
            string data = $"{e.KeyType} - {e.KeyCode}";
            byte[] byteData = Encoding.ASCII.GetBytes(data);
            byteSend[0] = type;
            byteSend[1] = isHost;

            Array.Copy(byteData, 0, byteSend, 2, byteData.Length);

            Client.Send(byteSend);
            //flag = false;
            //while (!flag)
            //{
            //    uint result = _keyboardSimulator.SendKey(keyReceived, ref flag);
            //    label1.Text = $"{result}";
            //    Thread.Sleep(10);
            //}
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Client = new TCPClient();
            _keyboardHook.Start();
            Connect();
        }
        private void From1_Closed(object sender, FormClosedEventArgs e)
        {
            _keyboardHook.Stop();
            if (Client != null)
            {
                Client.ImageReceived -= Callback;
            }
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
            string remoteHostName = "192.168.0.100";
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
