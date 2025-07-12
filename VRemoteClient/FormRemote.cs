using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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
        private KeyboardHook _keyboardHook;
        private GlobalMouseHook _mouseHook;
        public FormRemote(RemoteClient remoteClient, ConnectionInfo info)
        {
            InitializeComponent();
            Client = remoteClient;
            _info = info;
            KeyboardHook = new KeyboardHook();
            MouseHook = new GlobalMouseHook();

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
            //In form constructor or designer
            vPictureBox.Dock = DockStyle.Fill;
            vPictureBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
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
                    client.P2PChunksEventHandler -= ChunksEvent;
                }
                _remoteClient = value;
                client = _remoteClient;
                if(client != null)
                {
                    client.P2PScreenEventHandler += ScreenEvent;
                    client.P2PChunksEventHandler += ChunksEvent;
                }
            }
        }
        public KeyboardHook KeyboardHook
        {
            get => _keyboardHook;
            set
            {
                KeyboardHook keyboardHook = _keyboardHook;
                if (keyboardHook != null)
                {
                    keyboardHook.KeyPressed -= KeyPressedEventHandler;
                }
                _keyboardHook = value;
                keyboardHook = _keyboardHook;
                if (keyboardHook != null)
                {
                    keyboardHook.KeyPressed += KeyPressedEventHandler;
                }
            }
        }
        public GlobalMouseHook MouseHook
        {
            get => _mouseHook;
            set
            {
                GlobalMouseHook mouseHook = _mouseHook;
                if (mouseHook != null)
                {
                    mouseHook.MouseClick -= MouseClickEvent;
                    mouseHook.MouseMove -= MouseMoveEvent;
                }
                _mouseHook = value;
                mouseHook = _mouseHook;
                if (mouseHook != null)
                {
                    mouseHook.MouseClick += MouseClickEvent;
                    mouseHook.MouseMove += MouseMoveEvent;
                }
            }
        }

        #endregion
        #region Methods
        private void MouseMoveEvent(object sender, CustomMouseEventArgs e)
        {
            //int pictureboxWidth = vPictureBox.ClientSize.Width;
            //int pictureboxHeight = vPictureBox.ClientSize.Height;
            //string mouseCommandString = MouseHook.MouseEventToString(pictureboxWidth, pictureboxHeight, e.Button, e.Action, e.X, e.Y);
            //Client.Send(commandType: Models.Enums.CommandType.MouseMove, Encoding.ASCII.GetBytes(mouseCommandString));
        }
        private void MouseClickEvent(object sender, CustomMouseEventArgs e)
        {
            int pictureboxWidth = vPictureBox.ClientSize.Width;
            int pictureboxHeight = vPictureBox.ClientSize.Height;
            string mouseCommandString = MouseHook.MouseEventToString(pictureboxWidth, pictureboxHeight, e.Button, e.Action, e.X, e.Y);
            Client.Send(commandType: Models.Enums.CommandType.MouseClick, Encoding.ASCII.GetBytes(mouseCommandString));
        }
        private void KeyPressedEventHandler(object sender, KeyMessageEventArgs e)
        {
            string keyCommandString = KeyboardHook.KeyboardEventTostring(e.Command, e.KeyModifier, e.KeyCode, e.KeyType);
            Client.Send(commandType: Models.Enums.CommandType.Keyboard, Encoding.ASCII.GetBytes(keyCommandString));
        }
        private void FormRemote_Load(object sender, EventArgs e)
        {

        }
        private void FormRemote_Shown(object sender, EventArgs e)
        {
            uint pId = (uint)Process.GetCurrentProcess().Id;
            KeyboardHook.Start(pId);
            MouseHook.StartHook(pId);
        }
        private void FormRemote_FormClosed(object sender, FormClosedEventArgs e)
        {
            try
            {
                KeyboardHook?.Dispose();
                MouseHook?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Error disposing hooks in FormRemote_FormClosed: {Message}", ex.Message);
            }
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
        private void ChunksEvent(List<ScreenBlock> blocks)
        {
            if (blocks == null || blocks.Count == 0)
                return;

            Rectangle dirtyRegion = blocks[0].Rectangle;
            lock (_screenLock)
            {
                foreach (var block in blocks)
                {
                    try
                    {
                        using MemoryStream ms = new MemoryStream(Utils.Extensions.Decompress(block.Bytes));
                        using Bitmap chunkBitmap = new Bitmap(ms);
                        // draw on _curScreen
                        _screenGraphics.DrawImage(chunkBitmap, block.Rectangle);

                        // merge dirty region
                        dirtyRegion = Rectangle.Union(dirtyRegion, block.Rectangle);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Draw block error: " + ex.Message);
                    }
                }
            }

            // Invalidate last merge region
            if (this.InvokeRequired)
                this.BeginInvoke(new Action(() => vPictureBox.Invalidate(dirtyRegion)));
            else
                vPictureBox.Invalidate(dirtyRegion);
        }
        #endregion
        #region Event Handlers
        private void MouseWheelEventHandler(object sender, MouseEventArgs e)
        {
            //Console.WriteLine("Mouse Wheel Event Triggered");
        }

        private void MouseClickEventHandler(object sender, MouseEventArgs e)
        {
            //Console.WriteLine("Mouse Click Event Triggered");
        }

        private void MouseMoveEventHandler(object sender, MouseEventArgs e)
        {
            //Console.WriteLine("Mouse Move Event Triggered");
        }

        private void KeyUpEventHandler(object sender, KeyEventArgs e)
        {
            //string command =  string.Format("KeyUp:{0}", e.KeyCode.ToString());
            //Client.Send(commandType: Models.Enums.CommandType.Keyboard, Encoding.ASCII.GetBytes(command));
            //Console.WriteLine("Key Up Event Triggered: " + e.KeyCode.ToString());
        }

        private void KeyDownEventHandler(object sender, KeyEventArgs e)
        {
            //string command = string.Format("KeyDown:{0}", e.KeyCode.ToString());
            //Client.Send(commandType: Models.Enums.CommandType.Keyboard, Encoding.ASCII.GetBytes(command));
            //Console.WriteLine("Key Down Event Triggered: " + e.KeyCode.ToString());
        }
        #endregion
    }
}
