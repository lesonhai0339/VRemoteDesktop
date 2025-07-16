using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using VRemoteClient.Models.Entities;
using VRemoteClient.Models.Enums;
using VRemoteClient.Services;

namespace VRemoteClient
{
    public partial class FormRemote : Form
    {
        private int _clientWidth;
        private int _clientHeight;
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
            _clientHeight = info.Receiver.Height;
            _clientWidth = info.Receiver.Width;
            KeyboardHook = new KeyboardHook();
            MouseHook = new GlobalMouseHook();

            Text = _info.Receiver.Id.Trim();
            Icon = new Icon("Resources/logo.ico");

            // PictureBox
            vPictureBox.Dock = DockStyle.Fill;
            vPictureBox.Size = new Size(150, 150);
            base.AutoScaleDimensions = new SizeF(6f, 13f); 
            vPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            vPictureBox.BackColor = Color.Black;

            vPictureBox.MouseClick += MouseClickEventHandler;
            vPictureBox.MouseDoubleClick += MouseDbClickEventHandler;
            vPictureBox.MouseWheel += MouseWheelEventHandler;
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
                    //mouseHook.MouseClick -= MouseClickEvent;
                    //mouseHook.MouseMove -= MouseMoveEvent;
                }
                _mouseHook = value;
                mouseHook = _mouseHook;
                if (mouseHook != null)
                {
                    //mouseHook.MouseClick += MouseClickEvent;
                    //mouseHook.MouseMove += MouseMoveEvent;
                }
            }
        }

        #endregion
        #region Methods
/*        private void MouseMoveEvent(object sender, CustomMouseEventArgs e)
        {
        }
        private void MouseClickEvent(object sender, CustomMouseEventArgs e)
        {
        }*/
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
            // Lấy app process id và form windows handler để khởi tạo keyboard hook
            uint pId = (uint)Process.GetCurrentProcess().Id;
            IntPtr windowHandle = this.Handle; // Get the handle of formRemote
            KeyboardHook.Start(pId, windowHandle);
            //MouseHook.StartHook(pId);
        }
        private void FormRemote_FormClosed(object sender, FormClosedEventArgs e)
        {
            try
            {
                KeyboardHook?.Dispose();
                MouseHook?.Dispose();

                if(Client != null)
                {
                    Client.P2PScreenEventHandler -= ScreenEvent;
                    Client.P2PChunksEventHandler -= ChunksEvent;

                    Client = null;
                }
                this.Icon?.Dispose();
                if (vPictureBox != null)
                {
                    vPictureBox.MouseClick -= MouseClickEventHandler;
                    vPictureBox.MouseDoubleClick -= MouseDbClickEventHandler;
                    vPictureBox.MouseWheel -= MouseWheelEventHandler;

                    vPictureBox.Image?.Dispose();
                }
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
                lock (_screenLock)
                {
                    using (MemoryStream stream = new MemoryStream(data))
                    {
                        Bitmap image = (Bitmap)Image.FromStream(stream);

                        // Dispose old image to prevent memory leak
                        var oldImage = vPictureBox.Image;
                        _screenGraphics?.Dispose();
                        _curScreen?.Dispose();

                        _curScreen = new Bitmap(image);
                        var imageSize = image.Size;
                        Console.WriteLine(string.Format("Screen receive Width: {0}, Height: {1}", imageSize.Width, imageSize.Height));
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
            finally
            {
                Client.Send(Models.Enums.CommandType.ScreenOk, new byte[0]);
            }
        }
        private void ChunksEvent(List<ScreenBlock> blocks)
        {
            try
            {
                if (blocks == null || blocks.Count == 0)
                    return;
                Random rd = new Random();
                Rectangle dirtyRegion = blocks[0].Rectangle;
                lock (_screenLock)
                {
                    foreach (var block in blocks)
                    {
                        try
                        {
                            using MemoryStream ms = new MemoryStream(block.Bytes);
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
            finally
            {
                Client.Send(Models.Enums.CommandType.ScreenOk, new byte[0]);
            }
        }
        #endregion
        #region Event Handlers
        private void MouseWheelEventHandler(object sender, MouseEventArgs e)
        {
            int pictureboxWidth = vPictureBox.ClientSize.Width;
            int pictureboxHeight = vPictureBox.ClientSize.Height;
            string mouseCommandString = "";
            if (e.Delta > 0)
            {
                mouseCommandString = MouseHook.MouseEventToString("wheel_up", vPictureBox.Image.Width, vPictureBox.Image.Height, e);
            }
            else if (e.Delta < 0) 
            {
                mouseCommandString = MouseHook.MouseEventToString("wheel_down", vPictureBox.Image.Width, vPictureBox.Image.Height, e);
            }

            Client.Send(commandType: Models.Enums.CommandType.MouseMove, Encoding.ASCII.GetBytes(mouseCommandString));
        }

        private void MouseDbClickEventHandler(object sender, MouseEventArgs e)
        {
            //get actual mouse coordinate before send
            Point adjustedPoint = MouseHook.GetImagePointFromMouse(vPictureBox, e.X, e.Y);

            var adjustedMouseEventArgs = new MouseEventArgs(e.Button, e.Clicks, adjustedPoint.X, adjustedPoint.Y, e.Delta);

            //convert to string
            string mouseCommandString = MouseHook.MouseEventToString("", vPictureBox.Image.Width, vPictureBox.Image.Height, adjustedMouseEventArgs);
            Client.Send(commandType: Models.Enums.CommandType.MouseMove, Encoding.ASCII.GetBytes(mouseCommandString));
        }
        private void MouseClickEventHandler(object sender, MouseEventArgs e)
        {
            //get actual mouse coordinate before send
            Point adjustedPoint = MouseHook.GetImagePointFromMouse(vPictureBox, e.X, e.Y);

            var adjustedMouseEventArgs = new MouseEventArgs(e.Button, e.Clicks, adjustedPoint.X, adjustedPoint.Y, e.Delta);

            //convert to string
            string mouseCommandString = MouseHook.MouseEventToString("", vPictureBox.Image.Width, vPictureBox.Image.Height, adjustedMouseEventArgs);
            Client.Send(commandType: Models.Enums.CommandType.MouseMove, Encoding.ASCII.GetBytes(mouseCommandString));

        }
        #endregion
    }
}
