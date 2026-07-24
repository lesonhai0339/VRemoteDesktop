using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using Vsign4.VRemoteDesktop.Presenters;
using Vsign4.VRemoteDesktop.Services.SessionManagement;
using Vsign4.VRemoteDesktop.Services.RemoteDesktop;
using Vsign4.VRemoteDesktop.Services.Mouse.Enums;
using Vsign4.VRemoteDesktop.Events;
using Vsign4.VRemoteDesktop.Services.Keyboard.Enums;
using Vsign4.VRemoteDesktop.Services.ScreenCapture.Events;
using Vsign4.VRemoteDesktop.Utils;

namespace Vsign4.VRemoteDesktop.Views
{
    public partial class frmRemoteControl : Form
    {
        private const int MOUSE_MOVE_THROTTLE_MS = 20;
        private bool _disconnected = false; 
        private bool _isDrag;
        private int _clickCount;
        private Control _pendingSender;
        private MouseEventArgs _pendingClickArgs;
        private System.Windows.Forms.Timer _clickTimer;
        private DateTime _lastMouseMoveTime = DateTime.MinValue;

        private readonly RemotePresenter _remotePresenter;
        public frmRemoteControl(ClientSession clientSession, RemoteService remoteControlService)
        {
            InitializeComponent();
            this.StartPosition = FormStartPosition.CenterScreen;
            _isDrag = false;
            _remotePresenter = new RemotePresenter(clientSession, remoteControlService);

            //UI
            this.FormBorderStyle = FormBorderStyle.Fixed3D;
            base.AutoScaleDimensions = new SizeF(6f, 13f);
            this.Text = _remotePresenter.Id + " - " + _remotePresenter.Name;

            //DI
            _remotePresenter.UpdateScreen += UpdateScreenEventHandler;
            _remotePresenter.OnKeyboard += KeyboardReceivedEventHandler;
            _remotePresenter.OnDisconnect += DisconnectedEventHandler;

            // PictureBox
            vPictureBox.Dock = DockStyle.Fill;
            vPictureBox.Size = new Size(_remotePresenter.ClientWidth, _remotePresenter.ClientHeight);
            vPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            vPictureBox.BackColor = Color.Black;
            //vPictureBox.Setup(_vClient.Partner.Width, _vClient.Partner.Height, _remoteViewModel.Stride, _remoteViewModel.Bits, _remoteViewModel.BitmapInfo);
            //vPictureBox.Image = _remoteViewModel.Picture;
            vPictureBox.Image = _remotePresenter.Picture;
            vPictureBox.Paint += VPictureBox_Paint;
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
        private void UpdateScreenEventHandler(object sender, OnScreenEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<object, OnScreenEventArgs>(UpdateScreenEventHandler), sender, e);
                return;
            }

            var imgRect = ImageRectInControl();
            double scaleX = (double)imgRect.Width / vPictureBox.Image.Width;
            double scaleY = (double)imgRect.Height / vPictureBox.Image.Height;

            foreach (var rect in e.Rectangles)
            {
                // Dùng floor cho góc trên-trái và ceiling cho góc dưới-phải
                // để tránh khoảng hở giữa các rect liền kề do int truncation
                int x1 = (int)(rect.X * scaleX) + imgRect.X;
                int y1 = (int)(rect.Y * scaleY) + imgRect.Y;
                int x2 = (int)Math.Ceiling((rect.X + rect.Width) * scaleX) + imgRect.X;
                int y2 = (int)Math.Ceiling((rect.Y + rect.Height) * scaleY) + imgRect.Y;
                vPictureBox.Invalidate(new Rectangle(x1, y1, x2 - x1, y2 - y1));
            }

            vPictureBox.Update();
        }
        private Rectangle ImageRectInControl()
        {
            int ctrlWidth = vPictureBox.ClientSize.Width;
            int ctrlHeight = vPictureBox.ClientSize.Height;
            int imgWidth = vPictureBox.Image.Width;
            int imgHeight = vPictureBox.Image.Height;

            double scale =  Math.Min((double)ctrlWidth / imgWidth, (double)ctrlHeight / imgHeight);
            int dstWidth = (int)Math.Ceiling(imgWidth * scale);
            int dstHeight = (int)Math.Ceiling(imgHeight * scale);
            int offsetX = (int)((ctrlWidth - dstWidth) / 2);
            int offsetY = (int)((ctrlHeight - dstHeight) / 2);

            return new Rectangle(offsetX, offsetY, dstWidth, dstHeight);
        }
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

            Bitmap bmp = new Bitmap(_remotePresenter.ClientWidth, _remotePresenter.ClientHeight);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Black);
            }

            if (vPictureBox.Image != null)
            {
                vPictureBox.Image.Dispose();
            }

            if (_disconnected)
                // Already handled disconnection, no need to show message again 
                return;
            _disconnected = true;   
            vPictureBox.Image = bmp;
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
        private void frmRemoteControl_Load(object sender, EventArgs e)
        {

        }
        private void frmRemoteControl_Shown(object sender, EventArgs e)
        {
            StartFormKeyboardListener();
        }
        private void frmRemoteControl_FormClosing(object sender, FormClosingEventArgs e)
        {
            //Stop keyboard listener on this form
            StopFormKeyboardListener();

            //Unregister events
            if (_remotePresenter != null)
            {
                _remotePresenter.UpdateScreen -= UpdateScreenEventHandler;
                _remotePresenter.OnKeyboard -= KeyboardReceivedEventHandler;
                _remotePresenter.OnDisconnect -= DisconnectedEventHandler;
                _remotePresenter.Dispose();
            }

            //Form
            if (_pendingSender != null)
                _pendingSender.Dispose();

            if (_clickTimer != null)
                _clickTimer.Dispose();
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
                Logger.Log.ForContext("FileName", "FormRemote").Error(ex, "MouseMove event error");
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
            catch (Exception ex)
            {
                Logger.Log.ForContext("", this.GetType().Name).Error(ex, "KeyboardReceivedEventHandler error ");
            }
        }
        #endregion
    }
}
