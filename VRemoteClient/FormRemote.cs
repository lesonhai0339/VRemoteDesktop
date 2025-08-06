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
using VRemoteClient.Services.MouseService;
using VRemoteClient.Services.RemoteDesktopService;
using VRemoteClient.Services.ScreenService;
using VRemoteClient.Utils;
using static System.Net.Mime.MediaTypeNames;

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
        private RemoteDesktop _remoteDesktop;
        private ConnectionInfo _connectionInfo;
        private MouseHandler _mouseHook;
        private ScreenEventHandle _screenEventHandle;

        private DateTime _lastMouseMoveTime = DateTime.MinValue;
        private ManualResetEvent _isP2PDisconnectCallback;

        private System.Windows.Forms.Timer _clickTimer;
        private MouseEventArgs _pendingClickArgs;
        private Control _pendingSender;
        private int _clickCount;
        public FormRemote(RemoteDesktop remoteDesktop, ConnectionInfo info)
        {
            InitializeComponent();
            Init(remoteDesktop, info);

            _isDrag = false;
            _isP2PDisconnectCallback = new ManualResetEvent(false);

            //string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "logo.ico");
            //this.Icon = new Icon(iconPath);
            base.AutoScaleDimensions = new SizeF(6f, 13f);

            // PictureBox
            vPictureBox.Dock = DockStyle.Fill;
            vPictureBox.Size = new Size(info.Receiver.Width, info.Receiver.Height);
            vPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            vPictureBox.BackColor = Color.Black;

            vPictureBox.MouseWheel += MouseWheelEventHandler;
            vPictureBox.MouseMove += MouseMoveEvent;
            vPictureBox.MouseDown += MouseDownEventHandler;
            vPictureBox.MouseDown += MouseUpEventHandler;


            _clickTimer = new System.Windows.Forms.Timer();
            int interval = Math.Min(200, SystemInformation.DoubleClickTime / 2);
            _clickTimer.Interval = interval;
            _clickTimer.Tick += ClickTimer_Tick;
        }
        private void Init(RemoteDesktop remoteDesktop , ConnectionInfo info)
        {
            if(remoteDesktop == null || info == null || info.Receiver == null || info.Sender == null)
            {
                Log.ForContext("FileName", this.GetType().Name).Error("Args are null");
                MessageBox.Show("Xảy ra lỗi", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
                return;
            }
            RemoteDesktop ??= remoteDesktop;
            _connectionInfo ??= info;
            _screenEventHandle = new ScreenEventHandle();
            this.Text = _connectionInfo.Receiver.Id.Trim();

            MouseHook ??= new MouseHandler();
            RemoteDesktop.AddKeyboardHookByHandle(this.Handle);

            InitializeGraphicsSettings();

        }
        #region Properties
        public RemoteDesktop RemoteDesktop
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
                        _remoteDesktop.P2PDisconnect -= P2PDisconnectEvent;
                    }
                    _remoteDesktop = value;
                    if (_remoteDesktop != null)
                    {
                        _remoteDesktop.KeyboardEvent += KeyboardEvent;
                        _remoteDesktop.ScreenEvent += ScreenEvent;
                        _remoteDesktop.ChunksEvent += ChunksEvent;
                        _remoteDesktop.P2PDisconnect += P2PDisconnectEvent;

                    }
                }
            }
        }



        public MouseHandler MouseHook
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
        }
        private void FormRemote_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Cleanup keyboard hook
            try
            {
                try
                {
                    TaskObject disConnectTask = new TaskObject 
                    {
                        TaskType = Models.Enums.SocketDataType.P2PDisconnect,
                        SessionId = _connectionInfo.SessionId,
                        IsSendHeader = true
                    };

                    TryAddWork(disConnectTask);

                    if (!_isP2PDisconnectCallback.WaitOne(5000))
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
                if (_clickTimer != null)
                {
                    _clickTimer.Stop();
                    _clickTimer.Tick -= ClickTimer_Tick; // Unsubscribe from event
                    _clickTimer.Dispose();
                    _clickTimer = null;
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
                    _remoteDesktop.P2PDisconnect -= P2PDisconnectEvent;
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
            //clear picturebox
            try
            {
                if (vPictureBox != null)
                {
                    vPictureBox.MouseDown -= MouseDownEventHandler;
                    vPictureBox.MouseUp -= MouseUpEventHandler;
                    vPictureBox.MouseWheel -= MouseWheelEventHandler;
                    vPictureBox.MouseMove -= MouseMoveEvent;
                    vPictureBox.Image?.Dispose();
                    vPictureBox.Dispose();
                    vPictureBox = null;
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "FormRemote").Error(ex, "Error disposing PictureBox");
            }
            //clear unmanaged bitmap and graphic
            try
            {
                _curScreen?.Dispose();
                _screenGraphics?.Dispose();
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", "FormRemote").Error(ex, "Error disposing _curScreen and _screenGraphics");
            }
            finally
            {
                _curScreen = null;
                _screenGraphics = null;
            }
            _pendingSender?.Dispose();
            _isP2PDisconnectCallback?.Dispose();
            _isP2PDisconnectCallback = null;
            this.Dispose();
        }
        private void MouseDownEventHandler(object sender, MouseEventArgs e)
        {
            _pendingClickArgs = e;
            _pendingSender = sender as Control;
            _clickTimer.Stop();
            _clickTimer.Start();
            _clickCount++;
        }
        private void MouseUpEventHandler(object sender, MouseEventArgs e)
        {
            if (!_isDrag)
            {
                _clickCount++;

                _clickTimer.Stop();
                _clickTimer.Start();
            }
        }
        private void ClickTimer_Tick(object sender, EventArgs e)
        {
            _clickTimer.Stop();

            MouseEventType mouseType;
            switch (_clickCount)
            {
                case 2: mouseType = MouseEventType.Click; break;
                case 4: mouseType = MouseEventType.DoubleClick; break;
                case 6: mouseType = MouseEventType.TripleClick; break;
                default: mouseType = MouseEventType.None; break;
            }

            if (_pendingClickArgs != null && !_isDrag && mouseType != MouseEventType.None)
            {
                MouseHook.MouseEventToTask(
                    _connectionInfo.SessionId,
                    mouseType,
                    vPictureBox,
                    _pendingClickArgs
                );
            }
            _pendingClickArgs = null;
            _pendingSender = null;
            _clickCount = 0;
        }
        private void MouseMoveEvent(object sender, MouseEventArgs e)
        {
            try
            {
                //set delay
                DateTime now = DateTime.Now;
                if ((now - _lastMouseMoveTime).TotalMilliseconds < MOUSE_MOVE_THROTTLE_MS)
                    return; // Skip this event
                _lastMouseMoveTime = now;

                bool isLeftButtonDown = (Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left;

                MouseEventType mouseEvent = MouseEventType.DragAndDrop;
                WindowsMouseMessage mouseMessage = WindowsMouseMessage.None;

                if (isLeftButtonDown)
                {
                    if (!_isDrag)
                    {
                        mouseMessage = WindowsMouseMessage.DRAGDROP_MOUSEDOWN;
                        _isDrag = true;
                    }
                    else
                    {
                        mouseMessage = WindowsMouseMessage.DRAGDROP_MOUSEMOVE;
                    }
                }
                else
                {
                    if (!_isDrag)
                    {
                        mouseEvent = MouseEventType.Move;
                        mouseMessage = WindowsMouseMessage.DRAGDROP_MOUSEUP;
                       
                    }
                    else
                    {
                        mouseMessage = WindowsMouseMessage.DRAGDROP_MOUSEUP;
                        _isDrag = false;
                    }
                }

                MouseHook.MouseEventToTask(
                    _connectionInfo.SessionId,
                    mouseEvent,
                    vPictureBox,
                    e,
                    mouseMessage,
                    MouseState.Down
                );
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "FormRemote").Error(ex, "MouseMove event error");
            }
        }

        private void MouseWheelEventHandler(object sender, MouseEventArgs e)
        {
            MouseHook.MouseEventToTask(
                _connectionInfo.SessionId,
                MouseEventType.Wheel, 
                vPictureBox, 
                e
            );
        }  
        #endregion
        #region Event Handlers
        private void P2PDisconnectEvent(bool flag)
        {
            try
            {
                if (flag)
                {
                    _isP2PDisconnectCallback.Set();
                    Bitmap blackImage = new Bitmap(vPictureBox.Width, vPictureBox.Height);
                    vPictureBox.Image = blackImage;
                    MessageBox.Show("Đã ngắt kết nối", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
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
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<object, CustomKeyMessageEventArgs>(KeyboardEvent), sender, e);
                return;
            }

            string keyCommandString = "";
            if (e.Combination == KeyCombination.Copy)
            {
                keyCommandString = RemoteDesktop.GetClipboard();
            }
            else
            {
                if (e.Handle != this.Handle && Form.ActiveForm != this)
                {
                    return;
                }
                keyCommandString = RemoteDesktop.FormatKeyboardInput(e.Command, e.KeyModifier, e.KeyCode, e.KeyType);
            }

            //return if data is empty
            if (string.IsNullOrEmpty(keyCommandString)) return;

            TryAddWork(new TaskObject
            {
                TaskType = (e.Combination == KeyCombination.Copy) ? SocketDataType.Clipboard : SocketDataType.Keyboard,
                SessionId = _connectionInfo.SessionId,
                Data = Encoding.UTF8.GetBytes(keyCommandString)
            });
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
            if (!this.IsDisposed && !this.Disposing)
            {
                if (this.InvokeRequired)
                {
                    //cannnot using beginInvoke because need sure this call before chunks event
                    this.Invoke(new Action(()=>{
                        try
                        {
                            ScreenEvent(data);
                        }
                        catch(Exception ex)
                        {
                            Log.ForContext("FileName", "FormRemote").Error(ex, "ScreenEvent error");
                        }
                    }));
                    //this.Invoke(new Action<byte[]>(ScreenEvent), data);
                    return;
                }
            } 

            // UI thread code
            try
            {
                lock (_screenLock)
                {
                    var screenData = _screenEventHandle.RawScreenToScreenData(data);

                    Bitmap image =  _screenEventHandle.WriteToBitmap(screenData);

                    // Dispose old image to prevent memory leak
                    var oldImage = vPictureBox.Image;
                    _screenGraphics?.Dispose();
                    _curScreen?.Dispose();

                    _curScreen = new Bitmap(image);
                    _screenGraphics = Graphics.FromImage(_curScreen);

                    vPictureBox.Image = _curScreen;
                    oldImage?.Dispose();
                    image?.Dispose();
                }

                TryAddWork(new TaskObject
                {
                    TaskType = SocketDataType.ScreenOk
                });
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "FormRemote").Error(ex, "ScreenEvent error");
            }
        }
        private void ChunksEvent(byte[] data)
        {
            try
            {
                var regions = _screenEventHandle.RawChunksToRegions(data);

                if (regions == null || regions.Count == 0)
                    return;

                Rectangle rect =  _screenEventHandle.MergeRegions(_screenGraphics, regions);

                // Invalidate last merge region
                if(!this.IsDisposed && !this.Disposing)
                {
                    if (this.InvokeRequired)
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                InvalidateRegion(rect);
                            }
                            catch(Exception ex)
                            {
                                Log.ForContext("FileName", "FormRemote").Warning(ex, "InvalidateRegion error");
                            }
                        }));
                    }
                    else
                    {
                        InvalidateRegion(rect);
                    }
                }

                TryAddWork(new TaskObject
                {
                    TaskType = Models.Enums.SocketDataType.ChunksOk
                });
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
