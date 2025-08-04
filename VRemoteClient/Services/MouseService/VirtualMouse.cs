using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using VRemoteClient.Models.CustomEvents;
using VRemoteClient.Models.DTOs;
using VRemoteClient.Models.Enums;
using VRemoteClient.Utils;
using static VRemoteClient.Utils.Win32Apis;


namespace VRemoteClient.Services.MouseService
{
    public static class VirtualMouse
    {
        private const int WHEEL_DELTA = 120;

        #region Virtual mouse
        /// <summary>
        /// caculate scales between current physical screen dimesion and remote physical screen dimesion
        /// </summary>
        /// <param name="senderWidth">remote width</param>
        /// <param name="senderHeight">remote height</param>
        /// <param name="meWidth">current width</param>
        /// <param name="meHeight">current height</param>
        /// <returns><b>scaleX</b> and <b>scaleY</b></returns>
        private static Tuple<float, float> CalculateMouseCoordinate(int senderWidth, int senderHeight, int meWidth, int meHeight)
        {
            float scaleX = (float)meWidth / senderWidth;
            float scaleY = (float)meHeight / senderHeight;
            return new Tuple<float, float>(item1: scaleX, item2: scaleY);
        }
        public static MouseReceived BytesToCustomMouseEvent(byte[] data, int width, int height)
        {
            string[] mouseData = Encoding.ASCII.GetString(data).Trim().Split('|');
            if (mouseData.Length != 6)
            {
                Log.ForContext("FileName", "MouseHook").Error("Number of elements not exaclly");
            }
            int senderSceenWidth = int.Parse(mouseData[0]);
            int senderScreenHeight = int.Parse(mouseData[1]);
            int receiverScreenWidth = width;
            int receiverScreenHeight = height;
            WindowsMouseMessage button = (WindowsMouseMessage)int.Parse(mouseData[2]);
            MouseState action = (MouseState)int.Parse(mouseData[3]);
            int mouseX = int.Parse(mouseData[4]);
            int mouseY = int.Parse(mouseData[5]);

            return new MouseReceived
            {
                SenderWidth = senderSceenWidth,
                SenderHeight = senderScreenHeight,
                ReceiverWidth = receiverScreenWidth,
                ReceiverHeight = receiverScreenHeight,
                Button = button,
                Action = action,
                X = mouseX,
                Y = mouseY
            };
        }
        public static bool MouseEvent(MouseReceived mouseEvent)
        {
            Tuple<float, float> scales = CalculateMouseCoordinate(mouseEvent.SenderWidth, mouseEvent.SenderHeight, mouseEvent.ReceiverWidth, mouseEvent.ReceiverHeight);
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
                //left mouse dbclick
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
                // middle mouse dbclick
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
                // right mouse dbclick
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
                    if (mouseEvent.Action == MouseState.Up)
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
        /// <summary>
        /// Simulates a mouse wheel scroll event at a specified screen position.
        /// </summary>
        public static bool MouseWheel(float scaleX, float scaleY, int x, int y, int wheelDelta)
        {
            int pointX = (int)Math.Round(scaleX * x);
            int pointY = (int)Math.Round(scaleY * y);
            bool cusorFlag = MouseApis.SetCursorPos(pointX, pointY);
            if (!cusorFlag) return false;

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
            ////or you do not want use SetcursorPos, you can use this code
            //int normalizedX = x * 65535 / Screen.PrimaryScreen.Bounds.Width;
            //int normalizedY = y * 65535 / Screen.PrimaryScreen.Bounds.Height;
            ////and set 
            //inputs[0].u.mi.dx = normalizedX;
            //inputs[0].u.mi.dy = normalizedY;

            bool cusorFlag = MouseApis.SetCursorPos(pointX, pointY); // Set the cursor position to the specified coordinates
            if (!cusorFlag) return false;


            INPUT[] inputs = new INPUT[eventCount];

            for(int i = 0; i < eventCount; i++)
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
        }
        #endregion
    }
}
