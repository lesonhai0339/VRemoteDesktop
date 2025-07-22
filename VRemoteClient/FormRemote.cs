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
        private const int MOUSE_MOVE_THROTTLE_MS = 20;

        private bool _isDrag;
        private int _width;
        private int _height;

        private Bitmap _curScreen;
        private Graphics _screenGraphics;
        private RemoteClient _remoteClient;
        private ConnectionInfo _info;
        private KeyboardHook _keyboardHook;
        private GlobalMouseHook _mouseHook;

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
            _isDrag = false;
            KeyboardHook = new KeyboardHook();
            MouseHook = new GlobalMouseHook();

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
            clickTimer.Interval = Math.Max(50, SystemInformation.DoubleClickTime / 10);
            clickTimer.Tick += ClickTimer_Tick;
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
            AddMouseEventToTask(MouseEventType.DoubleClick, vPictureBox, e);
        }

        private void ClickTimer_Tick(object sender, EventArgs e)
        {
            clickTimer.Stop();
            AddMouseEventToTask(MouseEventType.Click, vPictureBox, pendingClickArgs);

            pendingClickArgs = null;
            pendingSender = null;
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
            get => _keyboardHook;
            set
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
        public GlobalMouseHook MouseHook
        {
            get => _mouseHook;
            set
            {
                _mouseHook = value;
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
            IntPtr windowHandle = this.Handle; // Get the handle of formRemote
            KeyboardHook.Start(pId, windowHandle);
            //MouseHook.StartHook(pId);
        }
        private void FormRemote_FormClosed(object sender, FormClosedEventArgs e)
        {
            try
            {
                try { KeyboardHook?.Dispose(); } catch (Exception ex) { Log.ForContext("FileName", "FormRemote").Fatal(ex, string.Format("Error disposing MouseHook: {0}", ex.Message)); }
                try { MouseHook?.Dispose(); } catch (Exception ex) { Log.ForContext("FileName", "FormRemote").Fatal(ex, string.Format("Error disposing MouseHook: {0}", ex.Message)); }

                try
                {
                    if (Client != null)
                    {
                        Client.P2PScreenEventHandler -= ScreenEvent;
                        Client.P2PChunksEventHandler -= ChunksEvent;
                        Client = null;
                    }
                }
                catch(Exception ex) { Log.ForContext("FileName", "FormRemote").Fatal(ex, string.Format("Error disposing MouseHook: {0}", ex.Message)); }
                try { Icon?.Dispose(); } catch (Exception ex) { Log.ForContext("FileName", "FormRemote").Fatal(ex, string.Format("Error disposing MouseHook: {0}", ex.Message)); }

                try
                {
                    if (vPictureBox != null)
                    {
                        vPictureBox.MouseClick -= MouseClickEventHandler;
                        vPictureBox.MouseDoubleClick -= MouseDbClickEventHandler;
                        vPictureBox.MouseWheel -= MouseWheelEventHandler;
                        vPictureBox.Image?.Dispose();
                    }
                }
                catch (Exception ex) { Log.ForContext("FileName", "FormRemote").Fatal(ex, string.Format("Error disposing MouseHook: {0}", ex.Message)); }
               
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "FormRemote").Fatal(ex, string.Format("Error disposing MouseHook: {0}", ex.Message));
            }
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
            string keyCommandString = KeyboardHook.KeyboardEventTostring(e.Command, e.KeyModifier, e.KeyCode, e.KeyType);

            TryAddWork(new TaskObject
            (
                taskType: Models.Enums.CommandType.Keyboard,
                data: Encoding.ASCII.GetBytes(keyCommandString)
            ));
        }
        #endregion
        #region Mouse
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
                        AddMouseEventToTask(MouseEventType.DragAndDrop, vPictureBox, e, MouseMessage.DRAGDROP_MOUSEDOWN, MouseType.Down);
                        _isDrag = true;
                    }
                    AddMouseEventToTask(MouseEventType.DragAndDrop, vPictureBox, e, MouseMessage.DRAGDROP_MOUSEMOVE, MouseType.Down);
                }
                else
                {
                    if (_isDrag)
                    {
                        AddMouseEventToTask(MouseEventType.DragAndDrop, vPictureBox, e, MouseMessage.DRAGDROP_MOUSEUP, MouseType.Down);
                        _isDrag = false;
                        return;
                    }
                    if (!_isDrag)
                    {
                        AddMouseEventToTask(MouseEventType.Move, vPictureBox, e, MouseMessage.WM_MOUSEMOVE, MouseType.Down);
                    }
                }
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", "FormRemote").Error(ex, "MouseMove event error");
            }
        }
        private void MouseWheelEventHandler(object sender, MouseEventArgs e)
        {
            AddMouseEventToTask(MouseEventType.Wheel, vPictureBox, e);
        }
        /// <summary>
        /// mouseEvent = 1(mouse click, db click)
        /// mouseEvent = 2(mouse wheel)
        /// mouseEvent = 3(mouse drag and drop)
        /// </summary>
        /// <param name="isMouseClick"></param>
        /// <param name="p"></param>
        /// <param name="e"></param>
        /// <param name="mouseMsg"></param>
        /// <param name="mouseType"></param>
        private void AddMouseEventToTask(MouseEventType mouseEvent, PictureBox p, MouseEventArgs e, MouseMessage mouseMsg = MouseMessage.None, MouseType mouseType = MouseType.None)
        {
            try
            {

                //check image nullable
                if (vPictureBox.Image == null) return;

                //get actual mouse coordinate before send
                Point adjustedPoint = MouseHook.GetImagePointFromMouse(p, e.X, e.Y);

                var adjustedMouseEventArgs = new MouseEventArgs(e.Button, e.Clicks, adjustedPoint.X, adjustedPoint.Y, e.Delta);

                string mouseEventString = MouseHook.MouseEventToString(mouseEvent, vPictureBox.Image.Width, vPictureBox.Image.Height, adjustedMouseEventArgs, mouseMsg, mouseType);

                if (string.IsNullOrEmpty(mouseEventString))
                    return;

                TryAddWork(new TaskObject(
                    taskType: Models.Enums.CommandType.Mouse,
                    data: Encoding.ASCII.GetBytes(mouseEventString)
                ));
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", "FormRemote").Error(ex, "MouseEvents error");
            }
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
                RectangleF displayRect = TransformImageToDisplay(rectangle);
                Rectangle rect = Rectangle.Round(displayRect);
                vPictureBox.Invalidate(rect);
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", "FormRemote").Error(ex, "InvalidateRegion error");
            }
        }
        /// <summary>
        /// Calculates the scaled rectangle coordinates for display in PictureBox,
        /// based on the original image rectangle, assuming PictureBox.SizeMode = Zoom.
        /// </summary>
        /// <param name="rectangle">Rectangle in image coordinates.</param>
        /// <returns>Rectangle transformed to display coordinates.</returns>
        private RectangleF TransformImageToDisplay(Rectangle rectangle)
        {
            try
            {
                if (vPictureBox.Image == null) return rectangle;


                var imageSize = vPictureBox.Image.Size;
                var pictureboxSize = vPictureBox.ClientSize;

                float scaleX = (float)pictureboxSize.Width / imageSize.Width;
                float scaleY = (float)pictureboxSize.Height / imageSize.Height;

                float scale = Math.Min(scaleX, scaleY);

                float displayWidth = (imageSize.Width * scale);
                float displayHeight = (imageSize.Height * scale);

                float offsetX = (pictureboxSize.Width - displayWidth) / 2;
                float offsetY = (pictureboxSize.Height - displayHeight) / 2;

                RectangleF displayRect = new RectangleF(
                    offsetX + (rectangle.X * scale),
                    offsetY + (rectangle.Y * scale),
                    (rectangle.Width * scale),
                    (rectangle.Height * scale));

                return displayRect;
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "FormRemote").Error(ex, "Error in TransformImageToDisplay");
                return rectangle;
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
