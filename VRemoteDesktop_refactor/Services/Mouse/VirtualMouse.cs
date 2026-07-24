using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Vsign4.VRemoteDesktop.Services.Mouse.DTOs;
using Vsign4.VRemoteDesktop.Services.Mouse.Enums;

namespace Vsign4.VRemoteDesktop.Services.Mouse
{
    internal static class VirtualMouse
    {
        private const int WHEEL_DELTA = 120;
        private const int MOUSE_MIN_FIELDS = 6;
        private const int MOUSE_PARTNET_WIDTH_INDEX = 0;
        private const int MOUSE_PARTNER_HEIGHT_INDEX = 1;
        private const int MOUSE_MESSAGE = 2;
        private const int MOUSE_ACTION = 3;
        private const int MOUSE_X = 4;
        private const int MOUSE_Y = 5;
        #region Virtual mouse
        /// <summary>
        /// Calculate scales between current physical screen dimension and remote physical screen dimension
        /// </summary>
        /// <param name="senderWidth">remote width</param>
        /// <param name="senderHeight">remote height</param>
        /// <param name="meWidth">current width</param>
        /// <param name="meHeight">current height</param>
        /// <returns><b>scaleX</b> and <b>scaleY</b></returns>
        private static Tuple<float, float> CalculateMouseCoordinate(int senderWidth, int senderHeight, int meWidth, int meHeight)
        {
            // Must be > 0: a zero sender dimension would divide by zero (float Infinity ->
            // overflow to int.MinValue downstream and wildly wrong SendInput coordinates).
            if (senderWidth <= 0 || senderHeight <= 0 || meWidth <= 0 || meHeight <= 0)
                return null;

            float scaleX = (float)meWidth / senderWidth;
            float scaleY = (float)meHeight / senderHeight;
            return new Tuple<float, float>(item1: scaleX, item2: scaleY);
        }
        public static MouseReceived BytesToCustomMouseEvent(byte[] data, int width, int height)
        {
            if (data == null || data.Length == 0)
                return null;

            if (width < 0 || height < 0)
                return null;

            string[] mouseData = Encoding.ASCII.GetString(data).Trim().Split('|');

            int partnerWidth;
            int partnerHeight;
            WindowsMouseMessage message;
            MouseAction action;
            int x;
            int y;
            if (mouseData.Length != MOUSE_MIN_FIELDS)
                return null;
            if (!int.TryParse(mouseData[MOUSE_PARTNET_WIDTH_INDEX], out partnerWidth)
                || partnerWidth < 0)
                return null;
            if (!int.TryParse(mouseData[MOUSE_PARTNER_HEIGHT_INDEX], out partnerHeight)
                || partnerHeight < 0)
                return null;
            if (!Enum.TryParse(mouseData[MOUSE_MESSAGE], out message)
                || !Enum.IsDefined(typeof(WindowsMouseMessage), message))
                return null;
            if (!Enum.TryParse(mouseData[MOUSE_ACTION], out action)
                || !Enum.IsDefined(typeof(MouseAction), action))
                return null;
            if (!int.TryParse(mouseData[MOUSE_X], out x)
                || x < 0)
                return null;
            if (!int.TryParse(mouseData[MOUSE_Y], out y)
                || y < 0)
                return null;

            return new MouseReceived
            {
                SenderWidth = partnerWidth,
                SenderHeight = partnerHeight,
                ReceiverWidth = width,
                ReceiverHeight = height,
                Button = message,
                Action = action,
                X = x,
                Y = y
            };
        }
        public static bool MouseEvent(MouseReceived mouseEvent)
        {

            Tuple<float, float> scales = CalculateMouseCoordinate(mouseEvent.SenderWidth, mouseEvent.SenderHeight, mouseEvent.ReceiverWidth, mouseEvent.ReceiverHeight);
            if (scales == null)
                return false;

            bool flag = false;
            List<WindowsMouseEvent> mouseEvents = new List<WindowsMouseEvent>();
            switch (mouseEvent.Button)
            {
                //left mouse click
                case WindowsMouseMessage.WM_LBUTTONDOWN:
                    mouseEvents.AddRange(
                        new List<WindowsMouseEvent>
                        {
                          WindowsMouseEvent.MOUSEEVENTF_LEFTDOWN,
                          WindowsMouseEvent.MOUSEEVENTF_LEFTUP
                        });
                    break;
                // middle mouse click
                case WindowsMouseMessage.WM_MBUTTONDOWN:
                    mouseEvents.AddRange(
                        new List<WindowsMouseEvent>
                        {
                          WindowsMouseEvent.MOUSEEVENTF_MIDDLEDOWN,
                          WindowsMouseEvent.MOUSEEVENTF_MIDDLEUP
                        });
                    break;
                // right mouse click
                case WindowsMouseMessage.WM_RBUTTONDOWN:
                    mouseEvents.AddRange(
                       new List<WindowsMouseEvent>
                       {
                         WindowsMouseEvent.MOUSEEVENTF_RIGHTDOWN,
                         WindowsMouseEvent.MOUSEEVENTF_RIGHTUP
                       });
                    break;
                //left mouse double click
                case WindowsMouseMessage.WM_LBUTTONDBLCLK:
                    mouseEvents.AddRange(
                       new List<WindowsMouseEvent>
                       {
                         WindowsMouseEvent.MOUSEEVENTF_LEFTDOWN,
                         WindowsMouseEvent.MOUSEEVENTF_LEFTUP,
                         WindowsMouseEvent.MOUSEEVENTF_LEFTDOWN,
                         WindowsMouseEvent.MOUSEEVENTF_LEFTUP
                       });
                    break;
                // middle mouse double click
                case WindowsMouseMessage.WM_MBUTTONDBLCLK:
                    mouseEvents.AddRange(
                        new List<WindowsMouseEvent>
                        {
                          WindowsMouseEvent.MOUSEEVENTF_MIDDLEDOWN,
                          WindowsMouseEvent.MOUSEEVENTF_MIDDLEUP,
                          WindowsMouseEvent.MOUSEEVENTF_MIDDLEDOWN,
                          WindowsMouseEvent.MOUSEEVENTF_MIDDLEUP
                        });
                    break;
                // right mouse double click
                case WindowsMouseMessage.WM_RBUTTONDBLCLK:
                    mouseEvents.AddRange(
                        new List<WindowsMouseEvent>
                        {
                          WindowsMouseEvent.MOUSEEVENTF_RIGHTDOWN,
                          WindowsMouseEvent.MOUSEEVENTF_RIGHTUP,
                          WindowsMouseEvent.MOUSEEVENTF_RIGHTDOWN,
                          WindowsMouseEvent.MOUSEEVENTF_RIGHTUP
                        });
                    break;
                // mouse wheel event
                case WindowsMouseMessage.WM_MOUSEWHEEL:
                    //wheel up
                    if (mouseEvent.Action == MouseAction.Up)
                    {
                        flag = MouseWheel(scales.Item1, scales.Item2, mouseEvent.X, mouseEvent.Y, +WHEEL_DELTA);
                    }
                    //wheel down
                    else
                    {
                        flag = MouseWheel(scales.Item1, scales.Item2, mouseEvent.X, mouseEvent.Y, -WHEEL_DELTA);
                    }
                    break;
                //mouse drag and drop
                case WindowsMouseMessage.DRAGDROP_MOUSEDOWN:
                    mouseEvents.Add(WindowsMouseEvent.MOUSEEVENTF_LEFTDOWN);
                    break;
                case WindowsMouseMessage.WM_MOUSEMOVE:
                case WindowsMouseMessage.DRAGDROP_MOUSEMOVE:
                    mouseEvents.Add(WindowsMouseEvent.MOUSEEVENTF_MOVE);
                    break;
                case WindowsMouseMessage.DRAGDROP_MOUSEUP:
                    mouseEvents.Add(WindowsMouseEvent.MOUSEEVENTF_LEFTUP);
                    break;
                //triple left click case
                case WindowsMouseMessage.WM_BUTTONTRIPLECLICK:
                    mouseEvents.AddRange(
                       new List<WindowsMouseEvent>
                       {
                          WindowsMouseEvent.MOUSEEVENTF_LEFTDOWN,
                          WindowsMouseEvent.MOUSEEVENTF_LEFTUP,
                          WindowsMouseEvent.MOUSEEVENTF_LEFTDOWN,
                          WindowsMouseEvent.MOUSEEVENTF_LEFTUP,
                          WindowsMouseEvent.MOUSEEVENTF_LEFTDOWN,
                          WindowsMouseEvent.MOUSEEVENTF_LEFTUP,
                       });
                    break;
                default:
                    break;
            }

            if (mouseEvents.Count == 0) return false;

            flag = MousePress(
                scaleX: scales.Item1,
                scaleY: scales.Item2,
                x: mouseEvent.X,
                y: mouseEvent.Y,
                mouseEvents: mouseEvents);

            return flag;
        }
        private static bool MousePress(float scaleX, float scaleY, int x, int y, List<WindowsMouseEvent> mouseEvents)
        {
            int pointX = (int)Math.Round(scaleX * x);
            int pointY = (int)Math.Round(scaleY * y);

            // Convert to normalized absolute coordinates (0-65535 range)
            int normalizedX = (pointX * 65535) / Screen.PrimaryScreen.Bounds.Width;
            int normalizedY = (pointY * 65535) / Screen.PrimaryScreen.Bounds.Height;

            int eventCount = mouseEvents.Count + 1; // +1 for the move event
            Interop.Win32Apis.INPUT[] inputs = new Interop.Win32Apis.INPUT[eventCount];

            // First input: MOVE cursor to position using ABSOLUTE coordinates
            inputs[0].type = Interop.Win32Apis.INPUT_MOUSE;
            inputs[0].u.mi.dwFlags = (uint)(WindowsMouseEvent.MOUSEEVENTF_MOVE |
                                             WindowsMouseEvent.MOUSEEVENTF_ABSOLUTE);
            inputs[0].u.mi.dx = normalizedX;
            inputs[0].u.mi.dy = normalizedY;
            inputs[0].u.mi.mouseData = 0;

            // Remaining inputs: Mouse click events
            for (int i = 0; i < mouseEvents.Count; i++)
            {
                inputs[i + 1].type = Interop.Win32Apis.INPUT_MOUSE;
                inputs[i + 1].u.mi.dwFlags = (uint)mouseEvents[i];
                inputs[i + 1].u.mi.dx = 0;  // Not needed when not using ABSOLUTE
                inputs[i + 1].u.mi.dy = 0;
                inputs[i + 1].u.mi.mouseData = 0;
            }

            // Send all inputs in one call
            uint flag = Interop.Win32Apis.WindowApis.SendInput((uint)eventCount, inputs, Marshal.SizeOf(typeof(Interop.Win32Apis.INPUT)));
            return flag == eventCount;
        }

        private static bool MouseWheel(float scaleX, float scaleY, int x, int y, int wheelDelta)
        {
            int pointX = (int)Math.Round(scaleX * x);
            int pointY = (int)Math.Round(scaleY * y);

            // Convert to normalized coordinates
            int normalizedX = (pointX * 65535) / Screen.PrimaryScreen.Bounds.Width;
            int normalizedY = (pointY * 65535) / Screen.PrimaryScreen.Bounds.Height;

            Interop.Win32Apis.INPUT[] inputs = new Interop.Win32Apis.INPUT[2];

            // First: Move cursor
            inputs[0].type = Interop.Win32Apis.INPUT_MOUSE;
            inputs[0].u.mi.dwFlags = (uint)(WindowsMouseEvent.MOUSEEVENTF_MOVE |
                                             WindowsMouseEvent.MOUSEEVENTF_ABSOLUTE);
            inputs[0].u.mi.dx = normalizedX;
            inputs[0].u.mi.dy = normalizedY;
            inputs[0].u.mi.mouseData = 0;

            // Second: Wheel scroll
            inputs[1].type = Interop.Win32Apis.INPUT_MOUSE;
            inputs[1].u.mi.dwFlags = (uint)WindowsMouseEvent.MOUSEEVENTF_WHEEL;
            inputs[1].u.mi.dx = 0;
            inputs[1].u.mi.dy = 0;
            inputs[1].u.mi.mouseData = unchecked((uint)wheelDelta);

            uint flag = Interop.Win32Apis.WindowApis.SendInput(2, inputs, Marshal.SizeOf(typeof(Interop.Win32Apis.INPUT)));
            return flag == 2;
        }

        /*  /// <summary>
          /// Simulates a mouse wheel scroll event at a specified screen position.
          /// </summary>
          private static bool MouseWheel(float scaleX, float scaleY, int x, int y, int wheelDelta)
          {
              int pointX = (int)Math.Round(scaleX * x);
              int pointY = (int)Math.Round(scaleY * y);
              bool cursorFlag = MouseApis.SetCursorPos(pointX, pointY);
              if (!cursorFlag) return false;

              INPUT[] inputs = new INPUT[1];
              inputs[0].type = INPUT_MOUSE;
              inputs[0].u.mi.dwFlags = (uint)WindowsMouseEvent.MOUSEEVENTF_WHEEL;
              inputs[0].u.mi.dx = 0;
              inputs[0].u.mi.dy = 0;
              inputs[0].u.mi.mouseData = unchecked((uint)wheelDelta);

              uint flag = WindowApis.SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
              return flag > 0;
          }
          /// <summary>
          /// Simulates a mouse press at the specified screen coordinates.
          /// </summary>
          private static bool MousePress(float scaleX, float scaleY, int x, int y, List<WindowsMouseEvent> mouseEvents)
          {
              int eventCount = mouseEvents.Count;
              int pointX = (int)Math.Round(scaleX * x);
              int pointY = (int)Math.Round(scaleY * y);
              ////or you do not want use SetcursorPos(), you can use this code
              //int normalizedX = x * 65535 / Screen.PrimaryScreen.Bounds.Width;
              //int normalizedY = y * 65535 / Screen.PrimaryScreen.Bounds.Height;
              ////and set 
              //inputs[0].u.mi.dx = normalizedX;
              //inputs[0].u.mi.dy = normalizedY;

              bool cursorFlag = MouseApis.SetCursorPos(pointX, pointY); // Set the cursor position to the specified coordinates
              if (!cursorFlag) return false;


              INPUT[] inputs = new INPUT[eventCount];

              for (int i = 0; i < eventCount; i++)
              {
                  inputs[i].type = INPUT_MOUSE;
                  inputs[i].u.mi.dwFlags = (uint)mouseEvents[i];
                  inputs[i].u.mi.dx = 0;
                  inputs[i].u.mi.dy = 0;
              }
              uint flag = WindowApis.SendInput((uint)eventCount, inputs, Marshal.SizeOf(typeof(INPUT)));
              if (flag != eventCount)
              {
                  return false;
              }
              return true;
          }*/
        #endregion
    }
}
