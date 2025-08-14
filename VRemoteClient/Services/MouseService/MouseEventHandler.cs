using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using VRemoteClient.Models.CustomEvents;
using VRemoteClient.Models.Entities;
using VRemoteClient.Models.Enums;
using VRemoteClient.Utils;
using static VRemoteClient.Utils.Win32Apis;

namespace VRemoteClient.Services.MouseService
{
    public class MouseHandler : IDisposable
    {
        private bool _disposed = false;
        public event EventHandler<CustomMouseTaskEventArgs> MouseTask;
        public MouseHandler()
        {
        }
        #region Methods 
        public void MouseEventToTask(string sessionId, MouseEventType mouseEvent, PictureBox p, MouseEventArgs e, WindowsMouseMessage mouseMsg = WindowsMouseMessage.None, MouseState mouseType = MouseState.None)
        {
            try
            {
                //check image nullable
                if (p.Image == null) return;

                //get actual mouse coordinate before send
                Point adjustedPoint = GetImagePointFromMouse(p, e.X, e.Y);

                var adjustedMouseEventArgs = new MouseEventArgs(e.Button, e.Clicks, adjustedPoint.X, adjustedPoint.Y, e.Delta);

                string mouseEventString = MouseEventToString(mouseEvent, p.Image.Width, p.Image.Height, adjustedMouseEventArgs, mouseMsg, mouseType);

                if (string.IsNullOrEmpty(mouseEventString))
                    return;

                MouseTask?.Invoke(this, new CustomMouseTaskEventArgs
                {
                    Task = new TaskObject
                    {
                        TaskType = SocketDataType.Mouse,
                        SessionId = sessionId,
                        Data = Encoding.ASCII.GetBytes(mouseEventString)
                    }
                });
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "FormRemote").Error(ex, "MouseEvents error");
            }
        }
        /// <summary>
        /// Calculates the scaled rectangle coordinates for display in PictureBox,
        /// based on the original image rectangle, assuming PictureBox.SizeMode = Zoom.
        /// </summary>
        /// <param name="rectangle">Rectangle in image coordinates.</param>
        /// <returns>Rectangle transformed to display coordinates.</returns>
        public RectangleF TransformImageToDisplay(PictureBox pictureBox, Rectangle rectangle)
        {
            try
            {
                if (pictureBox.Image == null) return rectangle;


                var imageSize = pictureBox.Image.Size;
                var pictureboxSize = pictureBox.ClientSize;

                float scaleX = (float)pictureboxSize.Width / imageSize.Width;
                float scaleY = (float)pictureboxSize.Height / imageSize.Height;

                float scale = Math.Min(scaleX, scaleY);

                float displayWidth = imageSize.Width * scale;
                float displayHeight = imageSize.Height * scale;

                float offsetX = (pictureboxSize.Width - displayWidth) / 2;
                float offsetY = (pictureboxSize.Height - displayHeight) / 2;

                RectangleF displayRect = new RectangleF(
                    offsetX + rectangle.X * scale,
                    offsetY + rectangle.Y * scale,
                    rectangle.Width * scale,
                    rectangle.Height * scale);

                return displayRect;
            }
            catch (Exception ex)
            {
                return new RectangleF(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
            }
        }
        public Point GetImagePointFromMouse(PictureBox pictureBox, int x, int y)
        {
            if (pictureBox.SizeMode == PictureBoxSizeMode.Zoom && pictureBox.Image != null)
            {
                float scaleX = (float)pictureBox.Width / pictureBox.Image.Width;
                float scaleY = (float)pictureBox.Height / pictureBox.Image.Height;

                float scale = Math.Min(scaleX, scaleY);

                int scaleWidth = (int)(pictureBox.Image.Width * scale);
                int scaleHeight = (int)(pictureBox.Image.Height * scale);

                int offsetX = (pictureBox.Width - scaleWidth) / 2;
                int offsetY = (pictureBox.Height - scaleHeight) / 2;

                if (x < offsetX || x > offsetX + scaleWidth ||
                   y < offsetY || y > offsetY + scaleHeight)
                {
                    return Point.Empty;
                }
                return new Point(
                    (int)Math.Round((x - offsetX) / scale),
                    (int)Math.Round((y - offsetY) / scale)
                );
            }
            else if (pictureBox.SizeMode == PictureBoxSizeMode.StretchImage && pictureBox.Image != null)
            {
                float scaleX = (float)pictureBox.Image.Width / pictureBox.Width;
                float scaleY = (float)pictureBox.Image.Height / pictureBox.Height;

                int scaleWidth = (int)(x * scaleX);
                int scaleHeight = (int)(y * scaleY);

                return new Point(scaleWidth, scaleHeight);
            }
            return new Point(x, y);
        }
        public string MouseEventToString(MouseEventType mouseEvent, int width, int height, MouseEventArgs e, WindowsMouseMessage mouseMsg = WindowsMouseMessage.None, MouseState mouseType = MouseState.None)
        {
            string result = "";
            switch (mouseEvent)
            {
                case MouseEventType.Click:
                    WindowsMouseMessage button = e.Button == MouseButtons.Left ? WindowsMouseMessage.WM_LBUTTONDOWN :
                                          e.Button == MouseButtons.Middle ? WindowsMouseMessage.WM_MBUTTONDOWN :
                                          e.Button == MouseButtons.Right ? WindowsMouseMessage.WM_RBUTTONDOWN :
                                          WindowsMouseMessage.None;
                    result = MouseEventToString(width, height, button, MouseState.Down, e.X, e.Y);
                    break;

                case MouseEventType.DoubleClick:
                    WindowsMouseMessage dbButton = e.Button == MouseButtons.Left ? WindowsMouseMessage.WM_LBUTTONDBLCLK :
                                         e.Button == MouseButtons.Middle ? WindowsMouseMessage.WM_MBUTTONDBLCLK :
                                         e.Button == MouseButtons.Right ? WindowsMouseMessage.WM_RBUTTONDBLCLK :
                                         WindowsMouseMessage.None;
                    result = MouseEventToString(width, height, dbButton, MouseState.Down, e.X, e.Y);
                    break;
                case MouseEventType.TripleClick:
                    WindowsMouseMessage tbButton = WindowsMouseMessage.WM_BUTTONTRIPLECLICK;
                    result = MouseEventToString(width, height, tbButton, MouseState.Down, e.X, e.Y);
                    break;
                case MouseEventType.Wheel:
                    if (e.Delta > 0)
                    {
                        result = MouseEventToString(width, height, WindowsMouseMessage.WM_MOUSEWHEEL, MouseState.Up, e.X, e.Y);
                    }
                    if (e.Delta < 0)
                    {
                        result = MouseEventToString(width, height, WindowsMouseMessage.WM_MOUSEWHEEL, MouseState.Down, e.X, e.Y);
                    }
                    break;

                case MouseEventType.Move:
                    result = MouseEventToString(width, height, WindowsMouseMessage.WM_MOUSEMOVE, MouseState.Down, e.X, e.Y);
                    break;

                case MouseEventType.DragAndDrop:
                    result = MouseEventToString(width, height, mouseMsg, mouseType, e.X, e.Y);
                    break;

                default:
                    break;

            }
            return result;
        }
        public string MouseEventToString(int width, int height, WindowsMouseMessage button, MouseState action, int x, int y)
        {
            return new StringBuilder()
                .Append(width).Append("|")
                .Append(height).Append("|")
                .Append((int)button).Append("|")
                .Append((int)action).Append("|")
                .Append(x).Append("|")
                .Append(y).ToString();
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    MouseTask = null;
                }
                _disposed = true;
            }
        }
        #endregion
    }
}
