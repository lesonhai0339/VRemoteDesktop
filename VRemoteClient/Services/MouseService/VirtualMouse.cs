using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using VRemoteClient.Models.CustomEvents;
using VRemoteClient.Models.Enums;
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
        #region Global MouseHook
        private static bool IsMouseHoveringOverTargetApp()
        {
            // Get the current mouse position
            Point cursorPos = Cursor.Position;

            // Get the window handle under the cursor
            IntPtr hwnd = WindowFromPoint(cursorPos);

            // Get the process ID of that window
            GetWindowThreadProcessId(hwnd, out uint pid);

            // Compare with your target process ID
            return pid == _targetProcessId;
        }
        public static void StartHook(uint pId)
        {
            _targetProcessId = pId;
            _hookID = SetWindowsHookEx(WH_MOUSE_LL, _proc,
                GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName), 0);

            if (_hookID == IntPtr.Zero)
            {
                throw new Exception("Failed to install mouse hook");
            }
        }
        public static void StopHook()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
        }
        /// <summary>
        /// Tracking mouse event and coordinate, currently catch four event( left click, right click, middle click and wheel). Can add more event in this list <see href="https://learn.microsoft.com/en-us/windows/win32/inputdev/mouse-input-notifications"/>
        /// </summary>
        /// <param name="nCode"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <returns></returns>
        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && IsMouseHoveringOverTargetApp())
            {
                // get mouse info
                MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));

                MouseMessage button = MouseMessage.None;
                MouseType action = MouseType.None;

                switch ((MouseMessage)(int)wParam)
                {

                    // left mouse click
                    case MouseMessage.WM_LBUTTONDOWN:
                        button = MouseMessage.WM_LBUTTONDOWN;
                        action = MouseType.Down;
                        break;
                    // middle mouse click
                    case MouseMessage.WM_MBUTTONDOWN:
                        button = MouseMessage.WM_MBUTTONDOWN;
                        action = MouseType.Down;
                        break;
                    // right mouse click
                    case MouseMessage.WM_RBUTTONDOWN:
                        button = MouseMessage.WM_RBUTTONDOWN;
                        action = MouseType.Down;
                        break;
                    // mouse wheel event
                    case MouseMessage.WM_MOUSEWHEEL:
                        button = MouseMessage.WM_MOUSEWHEEL;
                        action = MouseType.Down;
                        break;
                    default:
                        break;
                }

                var eventArgs = new CustomMouseEventArgs
                {
                    X = hookStruct.pt.x,
                    Y = hookStruct.pt.y,
                    Button = button,
                    Action = action
                };

                // Trigger events
                if (action == MouseType.Move)
                {
                    MouseMove?.Invoke(null, eventArgs);
                }
                else if (action == MouseType.Down || action == MouseType.Up)
                {
                    MouseClick?.Invoke(null, eventArgs);
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }
        #endregion
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
        public static bool MouseEvent(int senderWidth, int senderHeight, int meWidth, int meHeight, MouseMessage button, MouseType action, int x, int y)
        {
            Tuple<float, float> scales = CaculateMouseCorrdinate(senderWidth, senderHeight, meWidth, meHeight);
            bool flag = false;
            switch (button)
            {
                //left mouse click
                case MouseMessage.WM_LBUTTONDOWN:
                    flag = MousePress(
                        scaleX: scales.Item1,
                        scaleY: scales.Item2,
                        x: x,
                        y: y,
                        new List<uint>
                        {
                            MOUSEEVENTF_LEFTDOWN,
                            MOUSEEVENTF_LEFTUP
                        });
                    //flag = MousePress(scales.Item1, scales.Item2, MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP, x, y); //left mouse click
                    break;
                // middle mouse click
                case MouseMessage.WM_MBUTTONDOWN:
                    flag = MousePress(
                        scaleX: scales.Item1,
                        scaleY: scales.Item2,
                        x: x,
                        y: y,
                        new List<uint>
                        {
                            MOUSEEVENTF_MIDDLEDOWN,
                            MOUSEEVENTF_MIDDLEUP
                        });
                    //flag = MousePress(scales.Item1, scales.Item2, MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP, x, y); //left mouse click
                    break;
                // right mouse click
                case MouseMessage.WM_RBUTTONDOWN:
                    flag = MousePress(
                        scaleX: scales.Item1,
                        scaleY: scales.Item2,
                        x: x,
                        y: y,
                        new List<uint>
                        {
                            MOUSEEVENTF_RIGHTDOWN,
                            MOUSEEVENTF_RIGHTUP
                        });
                    //flag = MousePress(scales.Item1, scales.Item2, MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP, x, y); //left mouse click
                    break;
                //left mouse dbclick
                case MouseMessage.WM_LBUTTONDBLCLK:
                    flag = MousePress(
                       scaleX: scales.Item1,
                       scaleY: scales.Item2,
                       x: x,
                       y: y,
                       new List<uint>
                       {
                            MOUSEEVENTF_LEFTDOWN,
                            MOUSEEVENTF_LEFTUP,
                            MOUSEEVENTF_LEFTDOWN,
                            MOUSEEVENTF_LEFTUP
                       });
                    //MousePress(scales.Item1, scales.Item2, MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP, x, y); //left mouse click
                    //flag = MousePress(scales.Item1, scales.Item2, MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP, x, y); //left mouse click
                    break;
                // middle mouse dbclick
                case MouseMessage.WM_MBUTTONDBLCLK:
                    flag = MousePress(
                      scaleX: scales.Item1,
                      scaleY: scales.Item2,
                      x: x,
                      y: y,
                      new List<uint>
                      {
                            MOUSEEVENTF_MIDDLEDOWN,
                            MOUSEEVENTF_MIDDLEUP,
                            MOUSEEVENTF_MIDDLEDOWN,
                            MOUSEEVENTF_MIDDLEUP
                      });
                    //MousePress(scales.Item1, scales.Item2, MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP, x, y); //left mouse click
                    //flag = MousePress(scales.Item1, scales.Item2, MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP, x, y); //left mouse click
                    break;
                // right mouse dbclick
                case MouseMessage.WM_RBUTTONDBLCLK:
                    flag = MousePress(
                     scaleX: scales.Item1,
                     scaleY: scales.Item2,
                     x: x,
                     y: y,
                     new List<uint>
                     {
                            MOUSEEVENTF_RIGHTDOWN,
                            MOUSEEVENTF_RIGHTUP,
                            MOUSEEVENTF_RIGHTDOWN,
                            MOUSEEVENTF_RIGHTUP
                     });
                    //MousePress(scales.Item1, scales.Item2, MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP, x, y); //left mouse click
                    //flag = MousePress(scales.Item1, scales.Item2, MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP, x, y); //left mouse click
                    break;
                // mouse wheel event
                case MouseMessage.WM_MOUSEWHEEL:
                    //wheel up
                    if (action == MouseType.Up)
                    {
                        flag = MouseWheel(scales.Item1, scales.Item2, x, y, +120);
                    }
                    //wheel down
                    else
                    {
                        flag = MouseWheel(scales.Item1, scales.Item2, x, y, -120);
                    }
                    break;
                //mouse drag and drop
                case MouseMessage.DRAGDROP_MOUSEDOWN:
                    flag = SingleMouseEvent(scales.Item1, scales.Item2, MOUSEEVENTF_LEFTDOWN, x, y);
                    break;
                case MouseMessage.WM_MOUSEMOVE:
                case MouseMessage.DRAGDROP_MOUSEMOVE:
                    flag = SingleMouseEvent(scales.Item1, scales.Item2, MOUSEEVENTF_MOVE, x, y);
                    break;
                case MouseMessage.DRAGDROP_MOUSEUP:
                    flag = SingleMouseEvent(scales.Item1, scales.Item2, MOUSEEVENTF_LEFTUP, x, y);
                    break;
                //triple left click case
                case MouseMessage.WM_BUTTONTRIPLECLICK:
                    flag = MousePress(
                        scaleX: scales.Item1,
                        scaleY: scales.Item2,
                        x: x,
                        y: y,
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
            return flag;
        }
        /// <summary>
        /// Moves the mouse cursor to the scaled position and sends a single mouse event.
        /// Typically used in virtual drag or drawing operations where only one mouse event
        /// is needed (e.g., just Down, just Move, or just Up).
        /// </summary>
        /// <param name="scaleX">Scaling factor on the X-axis (relative to the original size).</param>
        /// <param name="scaleY">Scaling factor on the Y-axis (relative to the original size).</param>
        /// <param name="mouseEvent">Mouse event flag (e.g., MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_MOVE, etc.).</param>
        /// <param name="x">Original X coordinate (before scaling).</param>
        /// <param name="y">Original Y coordinate (before scaling).</param>
        /// <returns>True if the event was sent successfully; otherwise, false.</returns>
        public static bool SingleMouseEvent(float scaleX, float scaleY, uint mouseEvent, int x, int y)
        {
            int pointX = (int)Math.Round(scaleX * x);
            int pointY = (int)Math.Round(scaleY * y);
            bool cusorFlag = SetCursorPos(pointX, pointY);
            if (!cusorFlag) return false;

            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].u.mi.dwFlags = mouseEvent;
            inputs[0].u.mi.dx = 0;
            inputs[0].u.mi.dy = 0;

            uint flag = SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
            return flag > 0;
        }
        /// <summary>
        /// Simulates a mouse wheel scroll event at a specified screen position.
        /// </summary>
        /// <remarks>This method adjusts the target position based on the provided scaling factors and
        /// moves the cursor to the calculated position before simulating the mouse wheel event. If the cursor cannot be
        /// moved to the specified position, the method returns <see langword="false"/>.</remarks>
        /// <param name="scaleX">The horizontal scaling factor to adjust the x-coordinate.</param>
        /// <param name="scaleY">The vertical scaling factor to adjust the y-coordinate.</param>
        /// <param name="x">The x-coordinate of the target position, in unscaled screen coordinates.</param>
        /// <param name="y">The y-coordinate of the target position, in unscaled screen coordinates.</param>
        /// <param name="wheelDelta">The amount of wheel movement. Positive values indicate scrolling up, and negative values indicate scrolling
        /// down.</param>
        /// <returns><see langword="true"/> if the mouse wheel event was successfully simulated; otherwise, <see
        /// langword="false"/>.</returns>
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
        /// <remarks>This method adjusts the provided coordinates using the specified scaling factors to
        /// account for screen resolution differences, moves the cursor to the calculated position, and simulates a
        /// mouse button press and release.</remarks>
        /// <param name="scaleX">The horizontal scaling factor to adjust the x-coordinate to the screen resolution.</param>
        /// <param name="scaleY">The vertical scaling factor to adjust the y-coordinate to the screen resolution.</param>
        /// <param name="mouseDown">The mouse event flag representing the mouse button press (e.g., <see langword="MOUSEEVENTF_LEFTDOWN"/>).</param>
        /// <param name="mouseUp">The mouse event flag representing the mouse button release (e.g., <see langword="MOUSEEVENTF_LEFTUP"/>).</param>
        /// <param name="x">The x-coordinate of the target position, relative to the original resolution.</param>
        /// <param name="y">The y-coordinate of the target position, relative to the original resolution.</param>
        /// <returns><see langword="true"/> if the mouse press was successfully simulated; otherwise, <see langword="false"/>.</returns>
        private static bool MousePress(float scaleX, float scaleY, uint mouseDown, uint mouseUp, int x, int y)
        {
            int pointX = (int)Math.Round(scaleX * x);
            int pointY = (int)Math.Round(scaleY * y);

            bool cusorFlag = SetCursorPos(pointX, pointY); // Set the cursor position to the specified coordinates
            if (!cusorFlag) return false;

            ////or you do not want use SetcursorPos, you can use this code
            //int normalizedX = x * 65535 / Screen.PrimaryScreen.Bounds.Width;
            //int normalizedY = y * 65535 / Screen.PrimaryScreen.Bounds.Height;
            ////and set 
            //inputs[0].u.mi.dx = normalizedX;
            //inputs[0].u.mi.dy = normalizedY;

            INPUT[] inputs = new INPUT[2];

            inputs[0].type = INPUT_MOUSE;
            inputs[0].u.mi.dwFlags = mouseDown;
            inputs[0].u.mi.dx = 0;
            inputs[0].u.mi.dy = 0;

            inputs[1].type = INPUT_MOUSE;
            inputs[1].u.mi.dwFlags = mouseUp;
            inputs[1].u.mi.dx = 0;
            inputs[1].u.mi.dy = 0;

            uint flag = SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
            if (flag > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        private static bool MousePress(float scaleX, float scaleY, int x, int y, List<uint> mouseEvents)
        {
            int eventCount = mouseEvents.Count;
            int pointX = (int)Math.Round(scaleX * x);
            int pointY = (int)Math.Round(scaleY * y);

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
