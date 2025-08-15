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
using VRemoteDesktop.Services.Mouse;
using VRemoteDesktop.Services.ScreenCapture;
using VRemoteDesktop.ViewModels;
using VRemoteServer.Models;

namespace VRemoteDesktop.Views
{
    public partial class FormRemote : Form
    {
        private readonly object _screenLock = new object();
        private readonly object _lockObject = new object();
        private const int MOUSE_MOVE_THROTTLE_MS = 20;

        private ClientInfo _client;
        private RemoteViewModel _remoteViewModel;
        private MouseService _mouseService;
        private ScreenCaptureService _screenService;

        private bool _isDrag;

        private Bitmap _curScreen;
        private Graphics _screenGraphics;

        private DateTime _lastMouseMoveTime = DateTime.MinValue;
        private ManualResetEvent _isP2PDisconnectCallback;

        private System.Windows.Forms.Timer _clickTimer;
        private MouseEventArgs _pendingClickArgs;
        private Control _pendingSender;
        private int _clickCount;
        public FormRemote(ClientInfo client)
        {
            InitializeComponent();
            Client = client;
            RemoteViewModel = new RemoteViewModel(Client);
            _mouseService = new MouseService();
            _screenService = new ScreenCaptureService();

            _isDrag = false;
            _isP2PDisconnectCallback = new ManualResetEvent(false);

            base.AutoScaleDimensions = new SizeF(6f, 13f);

            // PictureBox
            vPictureBox.Dock = DockStyle.Fill;
            vPictureBox.Size = new Size(Client.Width, Client.Height);
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
        #region Properties
        public ClientInfo Client
        {
            get => _client;
            set => _client = value;
        }
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

            }
            _pendingClickArgs = null;
            _pendingSender = null;
            _clickCount = 0;
        }
        private void MouseMoveEvent(object sender, MouseEventArgs e)
        {
        }

        private void MouseWheelEventHandler(object sender, MouseEventArgs e)
        {
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
                RectangleF displayRect = _mouseService.TransformImageToDisplay(vPictureBox.Size, vPictureBox.Image.Size, rectangle);
                Rectangle rect = Rectangle.Round(displayRect);
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
