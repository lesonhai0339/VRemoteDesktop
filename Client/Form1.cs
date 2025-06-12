using FFMpegCore.Pipes;
using FFMpegCore;
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
using System.Threading.Tasks;
using System.Windows.Forms;
using static RemoteClient.Enums;
using static RemoteClient.WindowsScreen;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using FFMpegCore.Extensions.System.Drawing.Common;
using FFMpegCore.Enums;
using System.Drawing.Drawing2D;

namespace RemoteClient
{
    public partial class Form1 : Form
    {
        private ScreenCaptureSource _captureSource;    


        private const int IsRemote = 1;
        private TCPClient _tcpClient;
        //private KeyboardHook _keyboardHook;
        private KeyboardSendEventHandler _keyboardSendEventHandler;
        private KeyboardReceivedEventHandler _keyboardReceivedEventHandler;
        private KeyboardSimulator _keyboardSimulator;
        private ManualResetEvent resetEvent;
        private object _lock = new object();
        private WindowsScreen _windowsScreen;
        bool flag = false;


        public Form1()
        {
            InitializeComponent();
            //_keyboardHook = new KeyboardHook();
            _keyboardSimulator = new KeyboardSimulator();
            _keyboardSendEventHandler = new KeyboardSendEventHandler();
            _keyboardReceivedEventHandler = new KeyboardReceivedEventHandler();
            //_keyboardHook.KeyPressed += Form1_KeyDown;
            resetEvent = new ManualResetEvent(false);
            _windowsScreen = new WindowsScreen();
            _captureSource = new ScreenCaptureSource();
            this.KeyPreview = true;
            this.KeyDown += Form1_KeyDown;
            this.KeyUp += Form1_KeyUp;
            this.MouseMove += new MouseEventHandler(Form1_MouseMove);
            EnableMouseMoveForAllControls(this);

        }
        private void EnableMouseMoveForAllControls(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                control.MouseMove += Form1_MouseMove;

                // Recursively enable for child controls
                if (control.HasChildren)
                {
                    EnableMouseMoveForAllControls(control);
                }
            }
        }
        private void Form1_MouseMove(object sender, MouseEventArgs e)
        {
            Console.WriteLine($"X: {e.X} - Y: {e.Y}");
        }

        private void Form1_KeyUp(object sender, KeyEventArgs e)
        {
           
            if (e.KeyCode.GetType() == typeof(Keys))
            {
                Console.WriteLine($"Key Up: {e.KeyCode}");
                byte[] byteSend = new byte[1024];
                byte type = 0x02;
                byte isHost = (byte)IsRemote;
                byte[] sessionId = Encoding.ASCII.GetBytes("11111111");
                byte[] byteData = new byte[] { (byte)DataSendType.KEYBOARD, 0x01, (byte)e.KeyCode };
                byteSend[0] = type;
                byteSend[1] = isHost;
                Array.Copy(sessionId, 0, byteSend, 2, sessionId.Length);
                Array.Copy(byteData, 0, byteSend, 10, byteData.Length);
                string excutableTime = InvokeAction(delegate () { Client.Send(byteSend); }, resetEvent, 1);
            }

            if (e.Control && e.KeyCode.GetType() == typeof(Keys))
            {
                Console.WriteLine($"Key Up: {e.KeyCode}");
                byte[] byteSend = new byte[1024];
                byte type = 0x02;
                byte isHost = (byte)IsRemote;
                byte[] sessionId = Encoding.ASCII.GetBytes("11111111");
                byte[] byteData = new byte[] { (byte)DataSendType.KEYBOARD, 0x01, (byte)e.KeyCode };
                byteSend[0] = type;
                byteSend[1] = isHost;
                Array.Copy(sessionId, 0, byteSend, 2, sessionId.Length);
                Array.Copy(byteData, 0, byteSend, 10, byteData.Length);
                string excutableTime = InvokeAction(delegate () { Client.Send(byteSend); }, resetEvent, 1);
            }
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode.GetType() == typeof(Keys))
            {
                Console.WriteLine($"Key Down: {e.KeyCode}");
                byte[] byteSend = new byte[1024];
                byte type = 0x02;
                byte isHost = (byte)IsRemote;
                byte[] sessionId = Encoding.ASCII.GetBytes("11111111");
                byte[] byteData = new byte[] { (byte)DataSendType.KEYBOARD, 0x00,0x00, (byte)e.KeyCode };
                byteSend[0] = type;
                byteSend[1] = isHost;
                Array.Copy(sessionId, 0, byteSend, 2, sessionId.Length);
                Array.Copy(byteData, 0, byteSend, 10, byteData.Length);
                string excutableTime = InvokeAction(delegate () { Client.Send(byteSend); }, resetEvent, 1);
            }

            if (e.Control && e.KeyCode.GetType() == typeof(Keys))
            {
                Console.WriteLine($"Key Down: {e.KeyCode}");
                byte[] byteSend = new byte[1024];
                byte type = 0x02;
                byte isHost = (byte)IsRemote;
                byte[] sessionId = Encoding.ASCII.GetBytes("11111111");
                byte[] byteData = new byte[] { (byte)DataSendType.KEYBOARD, 0x00,0x01,(byte)Keys.LControlKey, (byte)e.KeyCode };
                byteSend[0] = type;
                byteSend[1] = isHost;
                Array.Copy(sessionId, 0, byteSend, 2, sessionId.Length);
                Array.Copy(byteData, 0, byteSend, 10, byteData.Length);
                string excutableTime = InvokeAction(delegate () { Client.Send(byteSend); }, resetEvent, 1);
            }
        }

        private async void Form1_Shown(object sender, EventArgs e)
        {
  
        }

        //private void Form1_KeyDown(object sender, KeyMessageEventArgs e)
        //{
        //    Console.WriteLine("Key press");
        //    label1.Text = $"{e.KeyCode} - {e.KeyType}";

        //    byte[] byteSend = new byte[1024];
        //    byte type = 0x02;
        //    byte isHost = (byte)IsRemote;
        //    byte[] sessionId = Encoding.ASCII.GetBytes("11111111");
        //    byte[] byteData = new byte[] { (byte)DataSendType.KEYBOARD, (byte)e.KeyType, (byte)e.KeyCode };
        //    byteSend[0] = type;
        //    byteSend[1] = isHost;
        //    Array.Copy(sessionId, 0, byteSend, 2, sessionId.Length);
        //    Array.Copy(byteData, 0, byteSend, 10, byteData.Length);
        //    string excutableTime = InvokeAction(delegate () { Client.Send(byteSend); }, resetEvent, 1);
        //    Console.WriteLine($"Eslaped Time: {excutableTime}");
        //}
        public async Task KeyboardHandler()
        {
            GlobalFFOptions.Configure(options => options.BinaryFolder = "C:\\Users\\haile\\Source\\Repos\\VRemoteDesktop\\Client\\FFmpeg\\ffmpeg_source");
            IEnumerable<IVideoFrame> CreateFrame(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    Bitmap bitmap = _captureSource.CaptureScreen();
                    yield return new BitmapVideoFrameWrapper(bitmap);
                }
            }
            while (true) // ← Loop vô tận
            {
                var videoFramesSource = new RawVideoPipeSource(CreateFrame(24)) // ← Chunks nhỏ
                {
                    FrameRate = 24
                };

                using (var networkStream = new NetworkStream(Client._sck))
                {

                    byte[] start = { 0x03, 0x01,0x01 };
                    await networkStream.WriteAsync(start, 0, 3);

                    bool flag = await FFMpegArguments
                        .FromPipeInput(videoFramesSource)
                        .OutputToPipe(new StreamPipeSink(networkStream), options => options
                            .WithVideoCodec("libx264")
                            .WithCustomArgument("-preset fast")
                            .WithCustomArgument("-tune zerolatency")
                            .WithCustomArgument("-crf 25")
                            .WithCustomArgument("-g 30")              // Keyframe every 30 frames
                            .WithCustomArgument("-keyint_min 30")
                            .WithCustomArgument("-sc_threshold 0")   // Disable scene change trigger
                            .WithCustomArgument("-threads 2")
                            .WithCustomArgument("-bufsize 512k")
                            .WithCustomArgument("-maxrate 750k")
                            .ForceFormat("mpegts"))                  // MPEG-TS container is stream-friendly
                        .ProcessAsynchronously();
                    Console.WriteLine(flag);
                    byte[] end = { 0x03, 0x01, 0x02 };
                    await networkStream.WriteAsync(end, 0, 3);
                }
            }
        }
        public string InvokeAction(Action action, ManualResetEvent resetEvent, int timeout= 10)
        {
            bool flag = false;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
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
            stopwatch.Stop();
            //return flag;
            return stopwatch.Elapsed.TotalSeconds.ToString("F3");
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            Client = new TCPClient(RemoteType.REMOTE);
            //_keyboardHook.Start();
            //Connect();
            //_windowsScreen.GrabDesktop();

        }
        private void From1_Closed(object sender, FormClosedEventArgs e)
        {
            //_keyboardHook.Stop();
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
