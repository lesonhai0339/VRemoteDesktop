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
using static VRemoteClient.Utils.Libraries;

namespace VRemoteClient.Services.MouseService
{
    public static class VirtualMouse
    {
        private static LowLevelMouseProc _proc;
        private static IntPtr _hookID = IntPtr.Zero;
        private static uint _targetProcessId;
        private static bool _disposed = false;

        public static event EventHandler<CustomMouseEventArgs> MouseClick;
        public static event EventHandler<CustomMouseEventArgs> MouseMove;
        #region Virtual mouse
        /// <summary>
        /// caculate scales between current physical screen dimesion and remote physical screen dimesion
        /// </summary>
        /// <param name="senderWidth">remote width</param>
        /// <param name="senderHeight">remote height</param>
        /// <param name="meWidth">current width</param>
        /// <param name="meHeight">current height</param>
        /// <returns><b>scaleX</b> and <b>scaleY</b></returns>
        private static Tuple<float, float> CaculateMouseCorrdinate(int senderWidth, int senderHeight, int meWidth, int meHeight)
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
            MouseMessage button = (MouseMessage)int.Parse(mouseData[2]);
            MouseType action = (MouseType)int.Parse(mouseData[3]);
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
            Tuple<float, float> scales = CaculateMouseCorrdinate(mouseEvent.SenderWidth, mouseEvent.SenderHeight, mouseEvent.ReceiverWidth, mouseEvent.ReceiverHeight);
            bool flag = false;
            List<uint> mouseEvents = new List<uint>();
            switch (mouseEvent.Button)
            {
                //left mouse click
                case MouseMessage.WM_LBUTTONDOWN:
                    mouseEvents.AddRange(
                        new List<uint>
                        {
                            MOUSEEVENTF_LEFTDOWN,
                            MOUSEEVENTF_LEFTUP
                        });
                    break;
                // middle mouse click
                case MouseMessage.WM_MBUTTONDOWN:
                    mouseEvents.AddRange(
                        new List<uint>
                        {
                            MOUSEEVENTF_MIDDLEDOWN,
                            MOUSEEVENTF_MIDDLEUP
                        });
                    break;
                // right mouse click
                case MouseMessage.WM_RBUTTONDOWN:
                    mouseEvents.AddRange(
                       new List<uint>
                       {
                             MOUSEEVENTF_RIGHTDOWN,
                            MOUSEEVENTF_RIGHTUP
                       });
                    break;
                //left mouse dbclick
                case MouseMessage.WM_LBUTTONDBLCLK:
                    mouseEvents.AddRange(
                       new List<uint>
                       {
                            MOUSEEVENTF_LEFTDOWN,
                            MOUSEEVENTF_LEFTUP,
                            MOUSEEVENTF_LEFTDOWN,
                            MOUSEEVENTF_LEFTUP
                       });
                    break;
                // middle mouse dbclick
                case MouseMessage.WM_MBUTTONDBLCLK:
                    mouseEvents.AddRange(
                        new List<uint>
                        {
                            MOUSEEVENTF_MIDDLEDOWN,
                            MOUSEEVENTF_MIDDLEUP,
                            MOUSEEVENTF_MIDDLEDOWN,
                            MOUSEEVENTF_MIDDLEUP
                        });
                    break;
                // right mouse dbclick
                case MouseMessage.WM_RBUTTONDBLCLK:
                    mouseEvents.AddRange(
                        new List<uint>
                        {
                            MOUSEEVENTF_RIGHTDOWN,
                            MOUSEEVENTF_RIGHTUP,
                            MOUSEEVENTF_RIGHTDOWN,
                            MOUSEEVENTF_RIGHTUP
                        });
                    break;
                // mouse wheel event
                case MouseMessage.WM_MOUSEWHEEL:
                    //wheel up
                    if (mouseEvent.Action == MouseType.Up)
                    {
                        flag = MouseWheel(scales.Item1, scales.Item2, mouseEvent.X, mouseEvent.Y, +120);
                    }
                    //wheel down
                    else
                    {
                        flag = MouseWheel(scales.Item1, scales.Item2, mouseEvent.X, mouseEvent.Y, -120);
                    }
                    break;
                //mouse drag and drop
                case MouseMessage.DRAGDROP_MOUSEDOWN:
                    mouseEvents.Add(MOUSEEVENTF_LEFTDOWN);
                    break;
                case MouseMessage.WM_MOUSEMOVE:
                case MouseMessage.DRAGDROP_MOUSEMOVE:
                    mouseEvents.Add(MOUSEEVENTF_MOVE);
                    break;
                case MouseMessage.DRAGDROP_MOUSEUP:
                    mouseEvents.Add(MOUSEEVENTF_LEFTUP);
                    break;
                //triple left click case
                case MouseMessage.WM_BUTTONTRIPLECLICK:
                    mouseEvents.AddRange(
                       new List<uint>
                       {
                            MOUSEEVENTF_LEFTDOWN,
                            MOUSEEVENTF_LEFTUP,
                            MOUSEEVENTF_LEFTDOWN,
                            MOUSEEVENTF_LEFTUP,
                            MOUSEEVENTF_LEFTDOWN,
                            MOUSEEVENTF_LEFTUP,
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
            bool cusorFlag = SetCursorPos(pointX, pointY);
            if (!cusorFlag) return false;

            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].u.mi.dwFlags = MOUSEEVENTF_WHEEL;
            inputs[0].u.mi.dx = 0;
            inputs[0].u.mi.dy = 0;
            inputs[0].u.mi.mouseData = unchecked((uint)wheelDelta);

            uint flag = SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
            return flag > 0;
        }
        /// <summary>
        /// Simulates a mouse press at the specified screen coordinates.
        /// </summary>
        private static bool MousePress(float scaleX, float scaleY, int x, int y, List<uint> mouseEvents)
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

            bool cusorFlag = SetCursorPos(pointX, pointY); // Set the cursor position to the specified coordinates
            if (!cusorFlag) return false;


            INPUT[] inputs = new INPUT[eventCount];

            for(int i = 0; i < eventCount; i++)
            {
                inputs[i].type = INPUT_MOUSE;
                inputs[i].u.mi.dwFlags = mouseEvents[i];
                inputs[i].u.mi.dx = 0;
                inputs[i].u.mi.dy = 0;
            }
            uint flag = SendInput((uint)eventCount, inputs, Marshal.SizeOf(typeof(INPUT)));
            if (flag > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        #endregion
    }
}
