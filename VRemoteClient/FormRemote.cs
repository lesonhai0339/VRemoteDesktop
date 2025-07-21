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
using System.Threading.Tasks;
using System.Windows.Forms;
using VRemoteClient.Models.Entities;
using VRemoteClient.Models.Enums;
using VRemoteClient.Services;

namespace VRemoteClient
{
    public partial class FormRemote : Form
    {
        private bool isMouseDragAndDrop = false;
        private int _width;
        private int _height;
        private Bitmap _curScreen;
        private Graphics _screenGraphics;
        private readonly object _screenLock = new object();
        private readonly object _chunksLock = new object();
        private RemoteClient _remoteClient;
        private ConnectionInfo _info;
        private KeyboardHook _keyboardHook;
        private GlobalMouseHook _mouseHook;
        private System.Threading.Timer _timer;
        public FormRemote(RemoteClient remoteClient, ConnectionInfo info)
        {
            InitializeComponent();
            Client = remoteClient;
            _info = info;
            KeyboardHook = new KeyboardHook();
            MouseHook = new GlobalMouseHook();

            //Text = _info.Receiver.Id.Trim();
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
            vPictureBox.MouseDown += MouseDownEvent;
            vPictureBox.MouseUp += MouseUpEvent;
            vPictureBox.MouseMove += MouseMoveEvent;
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
        private void test()
        {
            var screens = Utils.Capture.GetScreen();
            ScreenEvent(screens[0].Bytes);
        }
        private void KeyPressedEventHandler(object sender, KeyMessageEventArgs e)
        {
            string keyCommandString = KeyboardHook.KeyboardEventTostring(e.Command, e.KeyModifier, e.KeyCode, e.KeyType);

            Client.AddWork(new TaskObject
            (
                taskType: Models.Enums.CommandType.Keyboard,
                data: Encoding.ASCII.GetBytes(keyCommandString)
            ));
        }
        private void FormRemote_Load(object sender, EventArgs e)
        {

        }
        private void FormRemote_Shown(object sender, EventArgs e)
        {
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
                        _width = _curScreen.Width;
                        _height = _curScreen.Height;
                        var imageSize = image.Size;
                        _screenGraphics = Graphics.FromImage(_curScreen);

                        InitializeGraphicsSettings();

                        vPictureBox.Image = _curScreen;


                        oldImage?.Dispose();
                        image?.Dispose();
                    }
                }
                Client.AddWork(new TaskObject(
                     taskType: Models.Enums.CommandType.ScreenOk, 
                     data: new byte[0] 
                ));
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
            Rectangle dirtyRegion = new Rectangle(0,0, _width,_height);
            lock (_chunksLock)
            {
               for(int i=0;i< blocks.Count; i++)
               {
                    try
                    {
                        using MemoryStream ms = new MemoryStream(blocks[i].Bytes);
                        using Bitmap chunkBitmap = new Bitmap(ms);
                        // draw on _curScreen
                        _screenGraphics.DrawImage(chunkBitmap, blocks[i].Rectangle);


                        // merge dirty region
                        dirtyRegion = Rectangle.Union(dirtyRegion, blocks[i].Rectangle);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Draw block error: " + ex.Message);
                    }
                }
            }

            // Invalidate last merge region
            if (this.InvokeRequired)
                this.BeginInvoke(new Action(() => InvalidateRegion(dirtyRegion)));
            else
                InvalidateRegion(dirtyRegion);

            Client.AddWork(new TaskObject (
                taskType: Models.Enums.CommandType.ChunksOk, 
                data: new byte[0] 
            ));
        }
        #endregion
        #region Event Handlers

        private void MouseDownEvent(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isMouseDragAndDrop = true;

                //get actual mouse coordinate before send
                Point adjustedPoint = MouseHook.GetImagePointFromMouse(vPictureBox, e.X, e.Y);

                var adjustedMouseEventArgs = new MouseEventArgs(e.Button, e.Clicks, adjustedPoint.X, adjustedPoint.Y, e.Delta);

                //convert to string
                string mouseCommandString = MouseHook.ToString(vPictureBox.Image.Width, vPictureBox.Image.Height, MouseMessage.DRAGDROP_MOUSEDOWN, MouseType.Down, adjustedMouseEventArgs.X, adjustedMouseEventArgs.Y);

                Client.AddWork(new TaskObject(
                    taskType: Models.Enums.CommandType.MouseMove,
                    data: Encoding.ASCII.GetBytes(mouseCommandString)
                ));
            }
        }

        private void MouseUpEvent(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && isMouseDragAndDrop)
            {
                isMouseDragAndDrop = false;
                //get actual mouse coordinate before send
                Point adjustedPoint = MouseHook.GetImagePointFromMouse(vPictureBox, e.X, e.Y);

                var adjustedMouseEventArgs = new MouseEventArgs(e.Button, e.Clicks, adjustedPoint.X, adjustedPoint.Y, e.Delta);

                //convert to string
                string mouseCommandString = MouseHook.ToString(vPictureBox.Image.Width, vPictureBox.Image.Height, MouseMessage.DRAGDROP_MOUSEUP, MouseType.Down , adjustedMouseEventArgs.X, adjustedMouseEventArgs.Y);

                Client.AddWork(new TaskObject(
                    taskType: Models.Enums.CommandType.MouseMove,
                    data: Encoding.ASCII.GetBytes(mouseCommandString)
                ));
            }
        }

        private void MouseMoveEvent(object sender, MouseEventArgs e)
        {
            //mouse drag and drop
            if (isMouseDragAndDrop && e.Button == MouseButtons.Left)
            {
                //get actual mouse coordinate before send
                Point adjustedPoint = MouseHook.GetImagePointFromMouse(vPictureBox, e.X, e.Y);

                var adjustedMouseEventArgs = new MouseEventArgs(e.Button, e.Clicks, adjustedPoint.X, adjustedPoint.Y, e.Delta);

                //convert to string
                string mouseCommandString = MouseHook.ToString(vPictureBox.Image.Width, vPictureBox.Image.Height, MouseMessage.DRAGDROP_MOUSEMOVE, MouseType.Down, adjustedMouseEventArgs.X, adjustedMouseEventArgs.Y);

                Client.AddWork(new TaskObject(
                    taskType: Models.Enums.CommandType.MouseMove,
                    data: Encoding.ASCII.GetBytes(mouseCommandString)
                ));
            }        
        }
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
            
            Client.AddWork(new TaskObject (
                taskType: Models.Enums.CommandType.MouseMove, 
                data: Encoding.ASCII.GetBytes(mouseCommandString) 
            ));
        }

        private void MouseDbClickEventHandler(object sender, MouseEventArgs e)
        {
            //get actual mouse coordinate before send
            Point adjustedPoint = MouseHook.GetImagePointFromMouse(vPictureBox, e.X, e.Y);

            var adjustedMouseEventArgs = new MouseEventArgs(e.Button, e.Clicks, adjustedPoint.X, adjustedPoint.Y, e.Delta);

            //convert to string
            string mouseCommandString = MouseHook.MouseEventToString("", vPictureBox.Image.Width, vPictureBox.Image.Height, adjustedMouseEventArgs);
            
            Client.AddWork(new TaskObject ( 
                taskType: Models.Enums.CommandType.MouseMove, 
                data: Encoding.ASCII.GetBytes(mouseCommandString) 
            ));
        }
        private void MouseClickEventHandler(object sender, MouseEventArgs e)
        {
            //get actual mouse coordinate before send
            Point adjustedPoint = MouseHook.GetImagePointFromMouse(vPictureBox, e.X, e.Y);

            var adjustedMouseEventArgs = new MouseEventArgs(e.Button, e.Clicks, adjustedPoint.X, adjustedPoint.Y, e.Delta);

            //convert to string
            string mouseCommandString = MouseHook.MouseEventToString("", vPictureBox.Image.Width, vPictureBox.Image.Height, adjustedMouseEventArgs);
            
            Client.AddWork(new TaskObject (
                taskType: Models.Enums.CommandType.MouseMove, 
                data: Encoding.ASCII.GetBytes(mouseCommandString) 
            ));
        }
        #endregion
    }
}
