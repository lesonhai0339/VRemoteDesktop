using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteClient.Models.Entities;
using VRemoteClient.Services;

namespace VRemoteClient
{
    public partial class FormRemote : Form
    {
        private Bitmap _curScreen;
        private Graphics _screenGraphics;
        private readonly object _screenLock = new object();
        private RemoteClient _remoteClient;
        private ConnectionInfo _info;
        public FormRemote(RemoteClient remoteClient, ConnectionInfo info)
        {
            InitializeComponent();
            Client = remoteClient;
            _info = info;

            Text = _info.Receiver.Id.Trim();
            //Icon = new Icon("Resources/logo.ico");
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ClientSize = new Size(_info.Receiver.Width, _info.Receiver.Height);

            // Create and configure PictureBox
            vPictureBox.Size = new Size(_info.Receiver.Width, _info.Receiver.Height);
            vPictureBox.Location = new Point(0, 0);
            vPictureBox.SizeMode = PictureBoxSizeMode.StretchImage;

            KeyDown += KeyDownEventHandler;
            KeyUp += KeyUpEventHandler;
            MouseMove += MouseMoveEventHandler;
            MouseClick += MouseClickEventHandler;
            MouseWheel += MouseWheelEventHandler;
        }
        #region Properties
        public RemoteClient Client
        {
            get => _remoteClient;
            set
            {
                RemoteClient client = _remoteClient;
                if(client != null)
                {
                    client.P2PScreenEventHandler -= ScreenEvent;
                }
                _remoteClient = value;
                client = _remoteClient;
                if(client != null)
                {
                    client.P2PScreenEventHandler += ScreenEvent;
                }
            }
        }
        #endregion
        #region Methods
        private void FormRemote_Load(object sender, EventArgs e)
        {

        }
        private void InitializeGraphicsSettings()
        {
            //config graphics
            if (_screenGraphics != null)
            {
                _screenGraphics.CompositingMode = CompositingMode.SourceCopy;
                _screenGraphics.CompositingQuality = CompositingQuality.HighSpeed;
                _screenGraphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                _screenGraphics.SmoothingMode = SmoothingMode.None;
                _screenGraphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;
            }
        }
        private void InvalidateRegion(Rectangle rectangle)
        {
            vPictureBox.Invalidate(rectangle);
        }
        public void ScreenEvent(byte[] data)
        {
            // do chạy đồng bộ trên một luồng với socket nên bị bottleneck, cần chạy trên new thread
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<byte[]>(ScreenEvent), data);
                return;
            }

            // UI thread code
            try
            {
                byte[] dataDecompress = Utils.Extensions.Decompress(data);
                lock (_screenLock)
                {
                    using (MemoryStream stream = new MemoryStream(dataDecompress))
                    {
                        Bitmap image = (Bitmap)Image.FromStream(stream);

                        // Dispose old image to prevent memory leak
                        var oldImage = vPictureBox.Image;
                        _screenGraphics?.Dispose();
                        _curScreen?.Dispose();

                        _curScreen = new Bitmap(image);
                        _screenGraphics = Graphics.FromImage(_curScreen);

                        InitializeGraphicsSettings();

                        vPictureBox.Image = _curScreen;


                        oldImage?.Dispose();
                        image?.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ScreenEvent error: {ex.Message}");
            }
        }
        #endregion
        #region Event Handlers
        private void MouseWheelEventHandler(object sender, MouseEventArgs e)
        {
            Console.WriteLine("Mouse Wheel Event Triggered");
        }

        private void MouseClickEventHandler(object sender, MouseEventArgs e)
        {
            Console.WriteLine("Mouse Click Event Triggered");
        }

        private void MouseMoveEventHandler(object sender, MouseEventArgs e)
        {
            Console.WriteLine("Mouse Move Event Triggered");
        }

        private void KeyUpEventHandler(object sender, KeyEventArgs e)
        {
            Console.WriteLine("Key Up Event Triggered: " + e.KeyCode.ToString());
        }

        private void KeyDownEventHandler(object sender, KeyEventArgs e)
        {
            Console.WriteLine("Key Down Event Triggered: " + e.KeyCode.ToString());
        }
        #endregion
    }
}
