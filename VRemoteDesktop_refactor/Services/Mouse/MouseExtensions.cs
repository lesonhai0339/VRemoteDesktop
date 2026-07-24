using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Vsign4.VRemoteDesktop.Enums;
using Vsign4.VRemoteDesktop.Services.Mouse.DTOs;
using Vsign4.VRemoteDesktop.Services.Mouse.Enums;

namespace Vsign4.VRemoteDesktop.Services.Mouse
{
    public interface IMouseExtensions
    {
        RectangleF TransformImageToDisplay(Size sourceSize, Size imageSize, Rectangle rectangle);
        Point GetImagePointFromMouse(UISizeMode mode, Size sourceSize, Size imageSize, int x, int y);
        string MouseEventToString(MouseEventType mouseEvent, int width, int height, MouseData e, WindowsMouseMessage mouseMsg = WindowsMouseMessage.None, MouseAction mouseType = MouseAction.None);
        void Dispose();
    }
    public class MouseExtensions : IMouseExtensions, IDisposable
    {
        private bool _disposed = false;
        public MouseExtensions()
        {

        }
        /// <summary>
        /// Calculates the scaled rectangle coordinates for display in PictureBox,
        /// based on the original image rectangle, assuming PictureBox.SizeMode = Zoom.
        /// </summary>
        /// <param name="rectangle">Rectangle in image coordinates.</param>
        /// <returns>Rectangle transformed to display coordinates.</returns>
        public RectangleF TransformImageToDisplay(
            Size sourceSize,
            Size imageSize,
            Rectangle rectangle)
        {
            try
            {
                float scaleX = (float)sourceSize.Width / imageSize.Width;
                float scaleY = (float)sourceSize.Height / imageSize.Height;

                float scale = Math.Min(scaleX, scaleY);

                float displayWidth = imageSize.Width * scale;
                float displayHeight = imageSize.Height * scale;

                float offsetX = (sourceSize.Width - displayWidth) / 2;
                float offsetY = (sourceSize.Height - displayHeight) / 2;

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
        public Point GetImagePointFromMouse(
            UISizeMode mode,
            Size sourceSize,
            Size imageSize,
            int x,
            int y)
        {
            if (mode == UISizeMode.Zoom)
            {
                float scaleX = (float)sourceSize.Width / imageSize.Width;
                float scaleY = (float)sourceSize.Height / imageSize.Height;

                float scale = Math.Min(scaleX, scaleY);

                int scaleWidth = (int)(imageSize.Width * scale);
                int scaleHeight = (int)(imageSize.Height * scale);

                int offsetX = (sourceSize.Width - scaleWidth) / 2;
                int offsetY = (sourceSize.Height - scaleHeight) / 2;

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
            else if (mode == UISizeMode.StretchImage)
            {
                float scaleX = (float)imageSize.Width / sourceSize.Width;
                float scaleY = (float)imageSize.Height / sourceSize.Height;

                int scaleWidth = (int)(x * scaleX);
                int scaleHeight = (int)(y * scaleY);

                return new Point(scaleWidth, scaleHeight);
            }
            return new Point(x, y);
        }
        public string MouseEventToString(
            MouseEventType mouseEvent,
            int width,
            int height,
            MouseData e,
            WindowsMouseMessage mouseMsg = WindowsMouseMessage.None,
            MouseAction mouseType = MouseAction.None)
        {
            string result = "";
            switch (mouseEvent)
            {
                case MouseEventType.Click:
                    WindowsMouseMessage button = e.Button == VMouseButtons.Left ? WindowsMouseMessage.WM_LBUTTONDOWN :
                                          e.Button == VMouseButtons.Middle ? WindowsMouseMessage.WM_MBUTTONDOWN :
                                          e.Button == VMouseButtons.Right ? WindowsMouseMessage.WM_RBUTTONDOWN :
                                          WindowsMouseMessage.None;
                    result = Helpers.StringHelper.DataStringBuilder(width, height, button, MouseAction.Down, e.X, e.Y);
                    break;

                case MouseEventType.DoubleClick:
                    WindowsMouseMessage dbButton = e.Button == VMouseButtons.Left ? WindowsMouseMessage.WM_LBUTTONDBLCLK :
                                         e.Button == VMouseButtons.Middle ? WindowsMouseMessage.WM_MBUTTONDBLCLK :
                                         e.Button == VMouseButtons.Right ? WindowsMouseMessage.WM_RBUTTONDBLCLK :
                                         WindowsMouseMessage.None;
                    result = Helpers.StringHelper.DataStringBuilder(width, height, dbButton, MouseAction.Down, e.X, e.Y);
                    break;
                case MouseEventType.TripleClick:
                    WindowsMouseMessage tbButton = WindowsMouseMessage.WM_BUTTONTRIPLECLICK;
                    result = Helpers.StringHelper.DataStringBuilder(width, height, tbButton, MouseAction.Down, e.X, e.Y);
                    break;
                case MouseEventType.Wheel:
                    if (e.Delta > 0)
                    {
                        result = Helpers.StringHelper.DataStringBuilder(width, height, WindowsMouseMessage.WM_MOUSEWHEEL, MouseAction.Up, e.X, e.Y);
                    }
                    if (e.Delta < 0)
                    {
                        result = Helpers.StringHelper.DataStringBuilder(width, height, WindowsMouseMessage.WM_MOUSEWHEEL, MouseAction.Down, e.X, e.Y);
                    }
                    break;

                case MouseEventType.Move:
                    result = Helpers.StringHelper.DataStringBuilder(width, height, WindowsMouseMessage.WM_MOUSEMOVE, MouseAction.Down, e.X, e.Y);
                    break;

                case MouseEventType.DragAndDrop:
                    result = Helpers.StringHelper.DataStringBuilder(width, height, mouseMsg, mouseType, e.X, e.Y);
                    break;

                default:
                    break;

            }
            return result;
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
                }
                _disposed = true;
            }
        }
    }
}
