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
using System.Windows.Forms.VisualStyles;
using VRemoteClient.Models.CustomEvents;
using VRemoteClient.Models.Entities;
using VRemoteClient.Models.Enums;
using VRemoteClient.Services;
using VRemoteClient.Utils;

namespace VRemoteClient
{
    public partial class FormRemote : Form
    {
        private readonly object _screenLock = new object();
        private readonly object _lockObject = new object();
        private const int MOUSE_MOVE_THROTTLE_MS = 20;

        private bool _isDrag;
        private int _width;
        private int _height;

        private Bitmap _curScreen;
        private Graphics _screenGraphics;
        private RemoteClient _remoteClient;
        private ConnectionInfo _info;
        private KeyboardHook _keyboardHook;
        private MouseHook _mouseHook;

        private DateTime lastMouseMoveTime = DateTime.MinValue;

        private System.Windows.Forms.Timer clickTimer;
        private MouseEventArgs pendingClickArgs;
        private Control pendingSender;
        public FormRemote(RemoteClient remoteClient, ConnectionInfo info)
        {
            InitializeComponent();

            _info = info;
            _width = this.Width;
            _height = this.Height;

            Client = remoteClient;
            KeyboardHook = new KeyboardHook();
            MouseHook = new MouseHook();
            _isDrag = false;

            this.Text = _info.Receiver.Id.Trim();
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "logo.ico");
            this.Icon = new Icon(iconPath);
            base.AutoScaleDimensions = new SizeF(6f, 13f);

            // PictureBox
            vPictureBox.Dock = DockStyle.Fill;
            vPictureBox.Size = new Size(info.Receiver.Width, info.Receiver.Height);
            vPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            vPictureBox.BackColor = Color.Black;

            vPictureBox.MouseClick += MouseClickEventHandler;
            vPictureBox.MouseDoubleClick += MouseDbClickEventHandler;
            vPictureBox.MouseWheel += MouseWheelEventHandler;
            vPictureBox.MouseMove += MouseMoveEvent;

            clickTimer = new System.Windows.Forms.Timer();
            clickTimer.Interval = Math.Min(100, SystemInformation.DoubleClickTime / 5);
            clickTimer.Tick += ClickTimer_Tick;
        }
        #region Properties
        public RemoteClient Client
        {
            get => _remoteClient;
            set
            {
                if(_remoteClient != null)
                {
                    _remoteClient.P2PScreenEventHandler -= ScreenEvent;
                    _remoteClient.P2PChunksEventHandler -= ChunksEvent;
                }
                _remoteClient = value;
                if(_remoteClient != null)
                {
                    _remoteClient.P2PScreenEventHandler += ScreenEvent;
                    _remoteClient.P2PChunksEventHandler += ChunksEvent;
                }
            }
        }
        public KeyboardHook KeyboardHook
        {
            get
            {
                lock (_lockObject)
                {
                    return _keyboardHook;
                }
            }
            set
            {
                lock (_lockObject)
                {
                    if (_keyboardHook != null)
                    {
                        _keyboardHook.KeyPressed -= KeyPressedEventHandler;
                    }
                    _keyboardHook = value;
                    if (_keyboardHook != null)
                    {
                        _keyboardHook.KeyPressed += KeyPressedEventHandler;
                    }
                }
            }
        }
        public MouseHook MouseHook
        {
            get
            {
                lock (_lockObject)
                {
                    return _mouseHook;
                }
            }
            set
            {
                lock (_lockObject)
                {
                    if (_mouseHook != null)
                    {
                        _mouseHook.MouseTask -= MousePressedEventHandler;
                    }
                    _mouseHook = value;
                    if (_mouseHook != null)
                    {
                        _mouseHook.MouseTask += MousePressedEventHandler;
                    }
                }
            }
        }
        #endregion
        #region Methods
        private void FormRemote_Load(object sender, EventArgs e)
        {

        }
        private void FormRemote_Shown(object sender, EventArgs e)
        {
            uint pId = (uint)Process.GetCurrentProcess().Id;
            IntPtr windowHandle = this.Handle; ;
            KeyboardHook.Start(pId, windowHandle);
        }
        private void FormRemote_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Cleanup keyboard hook
            try
            {
                if (KeyboardHook != null)
                {
                    KeyboardHook.KeyPressed -= KeyPressedEventHandler;
                    KeyboardHook.Stop();
                    KeyboardHook.Dispose();
                    KeyboardHook = null; // Prevent double disposal
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "FormRemote").Error(ex, "Error disposing KeyboardHook");
            }

            // Cleanup mouse hook
            try
            {
                if (MouseHook != null)
                {
                    MouseHook.MouseTask -= MousePressedEventHandler;
                    MouseHook.Dispose();
                    MouseHook = null; // Prevent double disposal
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "FormRemote").Error(ex, "Error disposing MouseHook");
            }

            // Cleanup timer
            try
            {
                if (clickTimer != null)
                {
                    clickTimer.Stop();
                    clickTimer.Tick -= ClickTimer_Tick; // Unsubscribe from event
                    clickTimer.Dispose();
                    clickTimer = null;
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "FormRemote").Error(ex, "Error disposing Timer");
            }

            // Cleanup client
            try
            {
                if (Client != null)
                {
                    Client.P2PScreenEventHandler -= ScreenEvent;
                    Client.P2PChunksEventHandler -= ChunksEvent;
                    Client = null;
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "FormRemote").Error(ex, "Error disposing Client");
            }

            // Cleanup UI components
            try
            {
                Icon?.Dispose();
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "FormRemote").Error(ex, "Error disposing Icon");
            }

            try
            {
                if (vPictureBox != null)
                {
                    vPictureBox.MouseClick -= MouseClickEventHandler;
                    vPictureBox.MouseDoubleClick -= MouseDbClickEventHandler;
                    vPictureBox.MouseWheel -= MouseWheelEventHandler;
                    vPictureBox.MouseMove -= MouseMoveEvent;
                    vPictureBox.Image?.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "FormRemote").Error(ex, "Error disposing PictureBox");
            }
        }
        private void MouseClickEventHandler(object sender, MouseEventArgs e)
        {
            //waiting for double click called or timeout
            pendingClickArgs = e;
            pendingSender = sender as Control;
            clickTimer.Stop();
            clickTimer.Start();
        }
        private void MouseDbClickEventHandler(object sender, MouseEventArgs e)
        {
            clickTimer.Stop(); // Cancel pending click
            MouseHook.MouseEventToTask(MouseEventType.DoubleClick, vPictureBox, e);
        }
        private void MouseMoveEvent(object sender, MouseEventArgs e)
        {
            try
            {
                //set delay
                DateTime now = DateTime.Now;
                if ((now - lastMouseMoveTime).TotalMilliseconds < MOUSE_MOVE_THROTTLE_MS)
                    return; // Skip this event
                lastMouseMoveTime = now;

                bool isLeftButtonDown = (Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left;

                if (isLeftButtonDown)
                {
                    if (!_isDrag)
                    {
                        MouseHook.MouseEventToTask(MouseEventType.DragAndDrop, vPictureBox, e, MouseMessage.DRAGDROP_MOUSEDOWN, MouseType.Down);
                        _isDrag = true;
                    }
                    MouseHook.MouseEventToTask(MouseEventType.DragAndDrop, vPictureBox, e, MouseMessage.DRAGDROP_MOUSEMOVE, MouseType.Down);
                }
                else
                {
                    if (_isDrag)
                    {
                        MouseHook.MouseEventToTask(MouseEventType.DragAndDrop, vPictureBox, e, MouseMessage.DRAGDROP_MOUSEUP, MouseType.Down);
                        _isDrag = false;
                        return;
                    }
                    if (!_isDrag)
                    {
                        MouseHook.MouseEventToTask(MouseEventType.Move, vPictureBox, e, MouseMessage.WM_MOUSEMOVE, MouseType.Down);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "FormRemote").Error(ex, "MouseMove event error");
            }
        }
        private void MouseWheelEventHandler(object sender, MouseEventArgs e)
        {
            MouseHook.MouseEventToTask(MouseEventType.Wheel, vPictureBox, e);
        }
        private void ClickTimer_Tick(object sender, EventArgs e)
        {
            clickTimer.Stop();

            if (pendingClickArgs != null)
            {
                MouseHook.MouseEventToTask(MouseEventType.Click, vPictureBox, pendingClickArgs);
            }
            pendingClickArgs = null;
            pendingSender = null;
        }

        #endregion
        #region Event Handlers
        private void TryAddWork(TaskObject task)
        {
            if(task == null)
            {
                Log.ForContext("FileName", "FormRemote").Warning("task is null, cannot add work");
                return;
            }
            if (task.Data == null)
            {
                Log.ForContext("FileName", "FormRemote").Warning("task data is null, cannot add work");
                return;
            }
            if (Client == null)
            {
                Log.ForContext("FileName", "FormRemote").Warning("Client is null, cannot add work");
                return;
            }
            Client.AddWork(task);
        }
        #region Keyboard
        private void KeyPressedEventHandler(object sender, KeyMessageEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<object, KeyMessageEventArgs>(KeyPressedEventHandler), sender, e);
                return;
            }

            string keyCommandString = KeyboardHook.KeyboardEventTostring(e.Command, e.KeyModifier, e.KeyCode, e.KeyType);

            TryAddWork(new TaskObject
            (
                taskType: Models.Enums.CommandType.Keyboard,
                data: Encoding.ASCII.GetBytes(keyCommandString)
            ));
        }
        #endregion
        #region Mouse
        private void MousePressedEventHandler(object sender, CustomMouseTaskEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<object, CustomMouseTaskEventArgs>(MousePressedEventHandler), sender, e);
                return;
            }
            TryAddWork(e.Task);
        }
        #endregion
        #region Screen
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
            try
            {
                RectangleF displayRect = MouseHook.TransformImageToDisplay(vPictureBox ,rectangle);
                Rectangle rect = Rectangle.Round(displayRect);
                vPictureBox.Invalidate(rect);
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", "FormRemote").Error(ex, "InvalidateRegion error");
            }
        }
        public void ScreenEvent(byte[] data)
        {
            if (this.InvokeRequired)
            {
                //cannnot using beginInvoke because need sure this call before chunks event
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

                TryAddWork(new TaskObject(
                     taskType: Models.Enums.CommandType.ScreenOk,
                     data: new byte[0]
                ));
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "FormRemote").Error(ex, "ScreenEvent error");
            }
        }
        private void ChunksEvent(List<ScreenBlock> blocks)
        {
            try
            {
                if (blocks == null || blocks.Count == 0)
                    return;
                Rectangle dirtyRegion = Rectangle.Empty;
                lock (_screenLock)
                {
                    for (int i = 0; i < blocks.Count; i++)
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
                            Log.ForContext("FileName", "FormRemote").Warning(ex, "Chunks:Draw block error");
                        }
                    }
                }

                // Invalidate last merge region
                if (this.InvokeRequired)
                    this.BeginInvoke(new Action(() => InvalidateRegion(dirtyRegion)));
                else
                    InvalidateRegion(dirtyRegion);

                TryAddWork(new TaskObject(
                    taskType: Models.Enums.CommandType.ChunksOk,
                    data: new byte[0]
                ));
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", "FormRemote").Error(ex, "ChunksEvent error");
            }
        }
        #endregion
        #endregion
    }
}
