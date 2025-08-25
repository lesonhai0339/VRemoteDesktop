using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.Mouse;
using VRemoteDesktop.Services.RemoteDesktop;
using VRemoteDesktop.Services.ScreenCapture;
using VRemoteDesktop.Services.SystemService;
using VRemoteDesktop.Services.VTCPClient;
using VRemoteDesktop.ViewModels;
using VRemoteServer.Models;
using static VRemoteDesktop.Utils.Logger;

namespace VRemoteDesktop.Views
{
    public partial class FormRemote : Form
    {
        private readonly object _screenLock = new object();
        private readonly object _lockObject = new object();
        private const int MOUSE_MOVE_THROTTLE_MS = 20;

        private VClient _vClient;
        private RemoteViewModel _remoteViewModel;
        private RemoteDesktopService _remoteDesktopService;

        private readonly MouseExtensions _mouseExtension;
        private readonly ScreenCaptureExtensions _screenService;

        private bool _isDrag;
        private Bitmap _curScreen;
        private Graphics _screenGraphics;

        private DateTime _lastMouseMoveTime = DateTime.MinValue;
        private ManualResetEvent _isP2PDisconnectCallback;

        private System.Windows.Forms.Timer _clickTimer;
        private MouseEventArgs _pendingClickArgs;
        private Control _pendingSender;
        private int _clickCount;
        public FormRemote(VClient vClient, RemoteDesktopService remoteDesktopService)
        {
            InitializeComponent();
            _vClient = vClient;
            _mouseExtension = new MouseExtensions();
            _screenService = new ScreenCaptureExtensions();
            _remoteDesktopService = remoteDesktopService;
            _remoteDesktopService.KeyboardEvent += KeyboardReceivedEventHandler;
            RemoteViewModel = new RemoteViewModel(_vClient, _mouseExtension, _remoteDesktopService);
            this.FormBorderStyle = FormBorderStyle.Fixed3D;

            _isDrag = false;
            _isP2PDisconnectCallback = new ManualResetEvent(false);

            base.AutoScaleDimensions = new SizeF(6f, 13f);
            this.Text = _vClient.Partner.Id + " - "+ _vClient.Partner.ComputerName;
            // PictureBox
            vPictureBox.Dock = DockStyle.Fill;
            vPictureBox.Size = new Size(_vClient.Partner.Width, _vClient.Partner.Height);
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
        private void KeyboardReceivedEventHandler(object sender, KeyboardEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<object, KeyboardEventArgs>(KeyboardReceivedEventHandler), sender, e);
                return;
            }
            if (e.Combination == KeyCombination.Copy)
            {
                RemoteViewModel.GetClipboard(e);
            }
            else
            {
                if (e.Handle != this.Handle && Form.ActiveForm != this)
                {
                    return;
                }
                RemoteViewModel.ProcessKeyboard(e);
            }
        }
        #region Properties
        public RemoteViewModel RemoteViewModel
        {
            get
            {
                return _remoteViewModel;
            }
            set
            {
                if(_remoteViewModel != null)
                {
                    _remoteViewModel.ScreenEvent -= ScreenEvent;
                    _remoteViewModel.ScreenChunksEvent -= ChunksEvent;

                }
                _remoteViewModel = value;
                if (_remoteViewModel != null)
                {
                    _remoteViewModel.ScreenEvent += ScreenEvent;
                    _remoteViewModel.ScreenChunksEvent += ChunksEvent;

                }
            }
        }
        #endregion
        private void FormRemote_Load(object sender, EventArgs e)
        {

        }
        private void FormRemote_Shown(object sender, EventArgs e)
        {
            _remoteDesktopService.AddKeyboardListenerOnFormByHandle(this.Handle);
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
                RemoteViewModel.ProcessMouseEvent(
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

                RemoteViewModel.ProcessMouseEvent(
                    mouseEvent,
                    vPictureBox,
                    e,
                    mouseMessage,
                    MouseAction.Down
                );
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "FormRemote").Error(ex, "MouseMove event error");
            }
        }

        private void MouseWheelEventHandler(object sender, MouseEventArgs e)
        {
            RemoteViewModel.ProcessMouseEvent(
                MouseEventType.Wheel,
                vPictureBox,
                e
            );
        }
        #region Events
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
                RectangleF rectF = RemoteViewModel.TransformSize(vPictureBox.Size, vPictureBox.Image.Size, rectangle);
                Rectangle rect = Rectangle.Round(rectF);
                vPictureBox.Invalidate(rect);
            }
            catch (Exception ex)
            {
            }
        }
        public void ScreenEvent(byte[] data)
        {
            if (!this.IsDisposed && !this.Disposing)
            {
                if (this.InvokeRequired)
                {
                    //cannnot using beginInvoke because need sure this call before chunks event
                    this.Invoke(new Action(() => {
                        try
                        {
                            ScreenEvent(data);
                        }
                        catch (Exception ex)
                        {
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

                    var screenData = _screenService.RawScreenToScreenData(data);

                    Bitmap image = _screenService.WriteToBitmap(screenData);

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

            }
            catch (Exception ex)
            {
            }
        }
        private void ChunksEvent(byte[] data)
        {
            try
            {
                var regions = _screenService.RawChunksToRegions(data);

                if (regions == null || regions.Count == 0)
                    return;

                Rectangle rect = _screenService.MergeRegions(_screenGraphics, regions);

                // Invalidate last merge region
                if (!this.IsDisposed && !this.Disposing)
                {
                    if (this.InvokeRequired)
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                InvalidateRegion(rect);
                            }
                            catch (Exception ex)
                            {
                            }
                        }));
                    }
                    else
                    {
                        InvalidateRegion(rect);
                    }
                }

            }
            catch (Exception ex)
            {
            }
        }
        #endregion
    }
}
