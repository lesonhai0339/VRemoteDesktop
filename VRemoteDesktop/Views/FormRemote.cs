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
using VRemoteDesktop.Presenters;
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

        private readonly ClientSession _clientSession;
        private readonly RemotePresenter _remotePresenter;
        private readonly RemoteService _remoteControlService;

        private readonly IMouseExtensions _mouseExtension;

        private bool _isDrag;

        private DateTime _lastMouseMoveTime = DateTime.MinValue;
        private ManualResetEvent _isP2PDisconnectCallback;

        private System.Windows.Forms.Timer _clickTimer;
        private MouseEventArgs _pendingClickArgs;
        private Control _pendingSender;
        private int _clickCount;
        public FormRemote(ClientSession clientSession, RemoteService remoteControlService)
        {
            InitializeComponent();
            //this.MaximizeBox = false;
            //this.FormBorderStyle = FormBorderStyle.None;
            //this.WindowState = FormWindowState.Maximized;
            //Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
            //int newWidth = (int)(workingArea.Width * 0.7);
            //int newHeight = (int)(workingArea.Height * 0.7);
            //this.Size = new Size(newWidth, newHeight);
            //this.Location = new Point(
            //    workingArea.Left + (workingArea.Width - newWidth) / 2,
            //    workingArea.Top + (workingArea.Height - newHeight) / 2
            //);

            _clientSession = clientSession;
            _mouseExtension = new MouseExtensions();
            _remoteControlService = remoteControlService;
            _remotePresenter = new RemotePresenter(_clientSession, _remoteControlService, _mouseExtension);

#if DEBUG
            _remotePresenter.UpdateScreen += UpdateScreenEventHandler;
#endif
            _remotePresenter.OnKeyboard += KeyboardReceivedEventHandler;
            _remotePresenter.OnDisconnect += DisconnectedEventHandler;

            _isDrag = false;
            _isP2PDisconnectCallback = new ManualResetEvent(false);

            this.FormBorderStyle = FormBorderStyle.Fixed3D;
            base.AutoScaleDimensions = new SizeF(6f, 13f);
            this.Text = _clientSession.PartnerInfo.PartnerId + " - "+ _clientSession.PartnerInfo.ComputerName;

            // PictureBox
            vPictureBox.Dock = DockStyle.Fill;
            vPictureBox.Size = new Size(_clientSession.PartnerInfo.Width, _clientSession.PartnerInfo.Height);
            vPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            vPictureBox.BackColor = Color.Black;
#if DEBUG
            //vPictureBox.Setup(_vClient.Partner.Width, _vClient.Partner.Height, _remoteViewModel.Stride, _remoteViewModel.Bits, _remoteViewModel.BitmapInfo);
            //vPictureBox.Image = _remoteViewModel.Picture;
            vPictureBox.Image = _remotePresenter.Picture;
            vPictureBox.Paint += VPictureBox_Paint;
#endif

            vPictureBox.MouseWheel += MouseWheelEventHandler;
            vPictureBox.MouseMove += MouseMoveEvent;
            vPictureBox.MouseDown += MouseDownEventHandler;
            vPictureBox.MouseDown += MouseUpEventHandler;

            //timer
            _clickTimer = new System.Windows.Forms.Timer();
            int interval = Math.Min(200, SystemInformation.DoubleClickTime / 2);
            _clickTimer.Interval = interval;
            _clickTimer.Tick += ClickTimer_Tick;
        }

        private void VPictureBox_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.CompositingQuality = CompositingQuality.HighSpeed;
            e.Graphics.InterpolationMode = InterpolationMode.Low;
            e.Graphics.SmoothingMode = SmoothingMode.HighSpeed;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }
#if DEBUG
        private void UpdateScreenEventHandler(object sender, OnScreenEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<object, OnScreenEventArgs>(UpdateScreenEventHandler), sender, e);
                return;
            }
            //int dstX = (int)((e.Rectangle.X * vPictureBox.ImageScale) + vPictureBox.ImageOffsetX);
            //int dstY = (int)((e.Rectangle.Y * vPictureBox.ImageScale) + vPictureBox.ImageOffsetY);

            //int dstWidth = (int)Math.Ceiling(e.Rectangle.Width * vPictureBox.ImageScale);
            //int dstHeight = (int)Math.Ceiling(e.Rectangle.Height * vPictureBox.ImageScale);

            //Rectangle mergedRect = new Rectangle(dstX, dstY, dstWidth, dstHeight);

            var scaleWidth = (double)vPictureBox.ClientSize.Width / _clientSession.PartnerInfo.Width;
            var scaleHeight = (double)vPictureBox.ClientSize.Height / _clientSession.PartnerInfo.Height;
            var scale = Math.Min(scaleWidth, scaleHeight);


            int dstWidth = (int)Math.Ceiling(e.Rectangle.Width * scale);
            int dstHeight = (int)Math.Ceiling(e.Rectangle.Height * scale);

            int offsetX = (int)((vPictureBox.ClientSize.Width - dstWidth) / 2);
            int offsetY = (int)((vPictureBox.ClientSize.Height - dstHeight) / 2);

            int dstX = (int)((e.Rectangle.X * scale) + offsetX);
            int dstY = (int)((e.Rectangle.Y * scale) + offsetY);

            Rectangle mergedRect = new Rectangle(dstX, dstY, dstWidth, dstHeight);

            vPictureBox.Invalidate();
            vPictureBox.Update();
        }
#endif

        private void DisconnectedEventHandler(object sender, EventArgs e)
        {
            UpdateDisconnectUI();
        }
        private void UpdateDisconnectUI()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(UpdateDisconnectUI));
                return;
            }

            //Bitmap bmp = new Bitmap(_curScreen.Width, _curScreen.Height);

            //using (Graphics g = Graphics.FromImage(bmp))
            //{
            //    g.Clear(Color.Black);
            //}

            //if (vPictureBox.Image != null)
            //{
            //    vPictureBox.Image.Dispose();
            //}

            //vPictureBox.Image = bmp;
            var result = MessageBox.Show(
                "Mất kết nối đến đối tác",
                "Thông báo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );

            if (result == DialogResult.OK)
            {
                this.Close();
            }
        }
        #region Properties
        #endregion
        private void FormRemote_Load(object sender, EventArgs e)
        {

        }
        private void FormRemote_Shown(object sender, EventArgs e)
        {
            StartFormKeyboardListener();
        }
        private void FormRemote_FormClosing(object sender, FormClosingEventArgs e)
        {
            //Stop keyboard listener on form
            StopFormKeyboardListener();
            //Unregister events
            if (_remotePresenter != null)
            {
                _remotePresenter.UpdateScreen -= UpdateScreenEventHandler;
                _remotePresenter.OnKeyboard -= KeyboardReceivedEventHandler;
                _remotePresenter.OnDisconnect -= DisconnectedEventHandler;
            }
            //Form
            _pendingSender?.Dispose();
            _isP2PDisconnectCallback?.Dispose();
            _clickTimer?.Dispose();

            //DI
            _mouseExtension?.Dispose();
            _remotePresenter?.Dispose();
        }
        private void StartFormKeyboardListener()
        {
            _remotePresenter.AddKeyboardHook(this.Handle);
        }
        private void StopFormKeyboardListener()
        {
            _remotePresenter.RemoteKeyboardHook(this.Handle);
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
                _remotePresenter.ProcessMouseEvent(
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

                _remotePresenter.ProcessMouseEvent(
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
            _remotePresenter.ProcessMouseEvent(
                MouseEventType.Wheel,
                vPictureBox,
                e
            );
        }
        #region Events
        private void KeyboardReceivedEventHandler(object sender, KeyboardEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<object, KeyboardEventArgs>(KeyboardReceivedEventHandler), sender, e);
                return;
            }
            try
            {
                if (e.Combination == KeyCombination.Copy)
                {
                    _remotePresenter.GetClipboard(e);
                }
                else
                {
                    if (e.Handle != this.Handle && Form.ActiveForm != this)
                    {
                        return;
                    }
                    _remotePresenter.ProcessKeyboard(e);
                }
            }
            catch(Exception ex)
            {
                Log.ForContext("", this.GetType().Name).Error(ex, "KeyboardReceivedEventHandler error ");
            }
        }
        #endregion
    }
}
