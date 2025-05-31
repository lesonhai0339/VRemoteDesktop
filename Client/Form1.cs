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
using static RemoteClient.Enums;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace RemoteClient
{
    public partial class Form1 : Form
    {
        private TCPClient _tcpClient;
        private KeyboardHook _keyboardHook;
        private KeyboardSendEventHandler _keyboardSendEventHandler;
        private KeyboardReceivedEventHandler _keyboardReceivedEventHandler;
        private KeyboardSimulator _keyboardSimulator;
        private ManualResetEvent resetEvent;
        private object _lock = new object();    

        public Form1()
        {
            InitializeComponent();
            _keyboardHook = new KeyboardHook();
            _keyboardSimulator = new KeyboardSimulator();
            _keyboardSendEventHandler = new KeyboardSendEventHandler();
            _keyboardReceivedEventHandler = new KeyboardReceivedEventHandler();
            _keyboardHook.KeyPressed += Form1_KeyDown;
            resetEvent = new ManualResetEvent(false);

        }
        private void Form1_KeyDown(object sender, KeyMessageEventArgs e)
        {
            label1.Text = $"{e.KeyCode} - {e.KeyType}";
            byte[] byteKey = _keyboardSendEventHandler.KeyBuilder(e);
            Keys keyReceived = _keyboardReceivedEventHandler.KeyboardReceived(byteKey);


            //Note*: Freeze
            //byte[] byteSend = new byte[1024];
            //byte type = 0x02;
            //byte isHost = 0x01;

            //byte[] sessionId = Encoding.ASCII.GetBytes("11111111");

            //byte[] byteData = new byte[] { (byte)DataSendType.KEYBOARD, (byte)e.KeyType, (byte)e.KeyCode};
            //byteSend[0] = type;
            //byteSend[1] = isHost;
            
            //Array.Copy(sessionId, 0, byteSend, 2, sessionId.Length);
            //Array.Copy(byteData, 0, byteSend, 10, byteData.Length);


            //bool flag = InvokeAction(delegate() { Client.Send(byteSend);},resetEvent, 10);
            //if (!flag)
            //{
            //    Console.WriteLine("Send failed or timed out.");
            //}
        }
        public bool InvokeAction(Action action, ManualResetEvent resetEvent, int timeout= 10)
        {
            bool flag = false;
            lock (_lock)
            {
                resetEvent.Reset();
            }

            action();
            flag = resetEvent.WaitOne(timeout * 1000);
            lock (_lock)
            {
                resetEvent.Reset();
            }
            return flag;
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            Client = new TCPClient(RemoteType.CLIENT);
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
                    _tcpClient.DataResponseEvent -= (flag) =>
                    {
                        //Console.WriteLine($"DataResponseEvent: {flag}");
                        if (flag)
                        resetEvent.Set();
                    };
                }
                _tcpClient = value;
                if (_tcpClient != null)
                {
                    _tcpClient.ImageReceived += Callback;
                    _tcpClient.DataResponseEvent += (flag) =>
                    {
                        //Console.WriteLine($"DataResponseEvent: {flag}");
                        if (flag) resetEvent.Set();
                    };
                }
            }
        }
        private void Connect()
        {
            string remoteHostName = "27.0.12.78";//"192.168.0.101";
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
