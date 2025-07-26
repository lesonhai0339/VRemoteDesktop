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

        private Bitmap _curScreen;
        private Graphics _screenGraphics;
        private RemoteDesktopService _remoteDesktop;
        private ConnectionInfo _connectionInfo;
        private MouseHook _mouseHook;

        private DateTime lastMouseMoveTime = DateTime.MinValue;

        private System.Windows.Forms.Timer clickTimer;
        private MouseEventArgs pendingClickArgs;
        private Control pendingSender;
        private ManualResetEvent isP2PDisconnectCallback;
        public FormRemote(RemoteDesktopService remoteDesktop, ConnectionInfo info)
        {
            InitializeComponent();
            Init(remoteDesktop, info);

            _isDrag = false;
            isP2PDisconnectCallback = new ManualResetEvent(false);

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
        private void Init(RemoteDesktopService remoteDesktop , ConnectionInfo info)
        {
            if(remoteDesktop == null || info == null)
            {
                Log.ForContext("FileName", this.GetType().Name).Error("Args are null");
                MessageBox.Show("Xảy ra lỗi", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
                return;
            }
            RemoteDesktop ??= remoteDesktop;
            MouseHook ??= new MouseHook();
            _connectionInfo ??= info;
            this.Text = _connectionInfo.Receiver.Id.Trim();
            RemoteDesktop.AddKeyboardHookByHandle(this.Handle);
        }
        #region Properties
        public RemoteDesktopService RemoteDesktop
        {
            get
            {
                lock (_lockObject)
                {
                    return _remoteDesktop;
                }
            }
            set
            {
                lock (_lockObject)
                {
                    if (_remoteDesktop != null)
                    {
                        _remoteDesktop.KeyboardEvent -= KeyboardEvent;
                        _remoteDesktop.ScreenEvent -= ScreenEvent;
                        _remoteDesktop.ChunksEvent -= ChunksEvent;
                    }
                    _remoteDesktop = value;
                    if (_remoteDesktop != null)
                    {
                        _remoteDesktop.KeyboardEvent += KeyboardEvent;
                        _remoteDesktop.ScreenEvent += ScreenEvent;
                        _remoteDesktop.ChunksEvent += ChunksEvent;
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
            Console.WriteLine(this.Handle);
        }
        private void FormRemote_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Cleanup keyboard hook
            try
            {
                try
                {
                    TaskObject disConnectTask = new TaskObject(
                        taskType: Models.Enums.RemoteType.P2PDisconnect, 
                        receiveId: _connectionInfo.Receiver.Id,
                        receivePort: _connectionInfo.Receiver.Port,
                        data: new byte[0], 
                        isSendHeader: true);
                    TryAddWork(disConnectTask);

                    if (!isP2PDisconnectCallback.WaitOne(5000))
                    {
                        Log.ForContext("FileName", "FormRemote").Warning("P2P disconnect timed out after 5 seconds");
                    }
                }
                catch(Exception ex)
                {
                    Log.ForContext("FileName", "FormRemote").Error(ex, "Send P2PDisconnect error");
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
                if (_remoteDesktop != null)
                {
                    _remoteDesktop.RemoveKeyboardHookByHandle(this.Handle);
                    _remoteDesktop.KeyboardEvent -= KeyboardEvent;
                    _remoteDesktop.ScreenEvent -= ScreenEvent;
                    _remoteDesktop.ChunksEvent -= ChunksEvent;
                    _remoteDesktop = null;
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
            MouseHook.MouseEventToTask(
                _connectionInfo.Receiver.Id, 
                _connectionInfo.Receiver.Port, 
                MouseEventType.DoubleClick, 
                vPictureBox, 
                e
             );
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
                        MouseHook.MouseEventToTask(
                            _connectionInfo.Receiver.Id, 
                            _connectionInfo.Receiver.Port, 
                            MouseEventType.DragAndDrop, 
                            vPictureBox, 
                            e, 
                            MouseMessage.DRAGDROP_MOUSEDOWN, 
                            MouseType.Down
                        );
                        _isDrag = true;
                    }
                    MouseHook.MouseEventToTask(
                        _connectionInfo.Receiver.Id, 
                        _connectionInfo.Receiver.Port,
                        MouseEventType.DragAndDrop, 
                        vPictureBox, 
                        e, 
                        MouseMessage.DRAGDROP_MOUSEMOVE, 
                        MouseType.Down
                    );
                }
                else
                {
                    if (_isDrag)
                    {
                        MouseHook.MouseEventToTask(
                            _connectionInfo.Receiver.Id, 
                            _connectionInfo.Receiver.Port,
                            MouseEventType.DragAndDrop, 
                            vPictureBox, 
                            e, 
                            MouseMessage.DRAGDROP_MOUSEUP,
                            MouseType.Down
                        );
                        _isDrag = false;
                        return;
                    }
                    if (!_isDrag)
                    {
                        MouseHook.MouseEventToTask(_connectionInfo.Receiver.Id, 
                            _connectionInfo.Receiver.Port,
                            MouseEventType.Move,
                            vPictureBox, 
                            e, 
                            MouseMessage.WM_MOUSEMOVE,
                            MouseType.Down
                        );
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
            MouseHook.MouseEventToTask(
                _connectionInfo.Receiver.Id,
                _connectionInfo.Receiver.Port,
                MouseEventType.Wheel, 
                vPictureBox, 
                e
            );
        }
        private void ClickTimer_Tick(object sender, EventArgs e)
        {
            clickTimer.Stop();

            if (pendingClickArgs != null)
            {
                MouseHook.MouseEventToTask(
                    _connectionInfo.Receiver.Id, 
                    _connectionInfo.Receiver.Port,
                    MouseEventType.Click, 
                    vPictureBox, 
                    pendingClickArgs
                );
            }
            pendingClickArgs = null;
            pendingSender = null;
        }

        #endregion
        #region Event Handlers
        private void P2PDisconnectEvent()
        {
            try
            {
                isP2PDisconnectCallback.Set();
            }
            catch (ObjectDisposedException)
            {
                // Handle case where the ManualResetEvent was disposed during shutdown
                // This can happen if form disposal races with the disconnect event
            }
        }
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
            if (RemoteDesktop == null)
            {
                Log.ForContext("FileName", "FormRemote").Warning("RemoteDesktop is null, cannot add work");
                return;
            }
            RemoteDesktop.AddWork(task);
        }
        #region Keyboard
        private void KeyboardEvent(object sender, CustomKeyMessageEventArgs e)
        {
            if(e.Handle != this.Handle && Form.ActiveForm != this)
            {
                return;
            }
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<object, CustomKeyMessageEventArgs>(KeyboardEvent), sender, e);
                return;
            }

            string keyCommandString = RemoteDesktop.FormatKeyboardInput(e.Command, e.KeyModifier, e.KeyCode, e.KeyType);

            TryAddWork(new TaskObject
            (
                taskType: RemoteType.Keyboard,
                receiveId: _connectionInfo.Receiver.Id,
                receivePort: _connectionInfo.Receiver.Port,
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

                        var imageSize = image.Size;
                        _screenGraphics = Graphics.FromImage(_curScreen);

                        InitializeGraphicsSettings();

                        vPictureBox.Image = _curScreen;


                        oldImage?.Dispose();
                        image?.Dispose();
                    }
                }

                TryAddWork(new TaskObject(
                     taskType: Models.Enums.RemoteType.ScreenOk,
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
                    taskType: Models.Enums.RemoteType.ChunksOk,
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
