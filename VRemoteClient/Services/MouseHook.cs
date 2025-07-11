using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using static VRemoteClient.Utils.Libraries;

namespace VRemoteClient.Services
{
    public class GlobalMouseHook
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_MOUSEWHEEL = 0x020A;
        private LowLevelMouseProc _proc;
        private IntPtr _hookID = IntPtr.Zero;
        private uint _targetProcessId;

        public class MouseEventArgs : EventArgs
        {
            public int X { get; set; }
            public int Y { get; set; }
            public string Button { get; set; }
            public string Action { get; set; }
        }
        public event EventHandler<MouseEventArgs> MouseClick;
        public event EventHandler<MouseEventArgs> MouseMove;
        public GlobalMouseHook()
        {
            _proc = HookCallback;
        }
        private bool IsMouseHoveringOverTargetApp()
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

        public void StartHook(uint pId)
        {
            _targetProcessId = pId;
            _hookID = SetWindowsHookEx(WH_MOUSE_LL, _proc,
                GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName), 0);

            if (_hookID == IntPtr.Zero)
            {
                throw new Exception("Failed to install mouse hook");
            }

            Console.WriteLine("Mouse hook installed successfully");
        }

        public void StopHook()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
                Console.WriteLine("Mouse hook removed");
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && IsMouseHoveringOverTargetApp())
            {
                // Lấy thông tin mouse
                MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));

                string button = "";
                string action = "";

                switch ((int)wParam)
                {
                    case WM_LBUTTONDOWN:
                        button = "Left";
                        action = "Down";
                        break;
                    case WM_LBUTTONUP:
                        button = "Left";
                        action = "Up";
                        break;
                    case WM_RBUTTONDOWN:
                        button = "Right";
                        action = "Down";
                        break;
                    case WM_RBUTTONUP:
                        button = "Right";
                        action = "Up";
                        break;
                    case WM_MOUSEMOVE:
                        button = "None";
                        action = "Move";
                        break;
                    case WM_MOUSEWHEEL:
                        button = "Wheel";
                        action = "Scroll";
                        break;
                }

                // Tạo event args
                var eventArgs = new MouseEventArgs
                {
                    X = hookStruct.pt.x,
                    Y = hookStruct.pt.y,
                    Button = button,
                    Action = action
                };

                // Trigger events
                if (action == "Move")
                {
                    MouseMove?.Invoke(this, eventArgs);
                }
                else if (action == "Down" || action == "Up")
                {
                    MouseClick?.Invoke(this, eventArgs);
                    Console.WriteLine($"Mouse {button} {action} at ({hookStruct.pt.x}, {hookStruct.pt.y})");
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }
    }
    /*    public static class MouseHook
        {
            /// <summary>
            /// this for windows api
            /// </summary>
            /// <param name="x"></param>
            /// <param name="y"></param>
            /// <returns></returns>
            const int INPUT_MOUSE = 0;
            const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
            const uint MOUSEEVENTF_LEFTUP = 0x0004;
            const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
            const uint MOUSEEVENTF_RIGHTUP = 0x0010;
            const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
            const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
            const uint MOUSEEVENTF_WHEEL = 0x0800;
            const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

            public static bool MouseEvent(int messageMouseType, int x, int y)
            {
                bool flag = false;
                switch (messageMouseType)
                {
                    case WM_LBUTTONDOWN:
                        flag = MouseClick(MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP, x, y); //left mouse click
                        break;
                    case WM_LBUTTONDBLCLK:
                        flag = MouseClick(MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP, x, y);
                        if (flag)
                        {
                            flag = MouseClick(MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP, x, y);
                        }
                        break;
                    case WM_MBUTTONDOWN:
                        flag = MouseClick(MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP, x, y); //middle mouse click
                        break;
                    case WM_RBUTTONDOWN:
                        flag = MouseClick(MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP, x, y); //right mouse click
                        break;
                    case WM_MOUSEWHEEL:
                        flag = MouseClick(MOUSEEVENTF_WHEEL, MOUSEEVENTF_WHEEL, x, y); //mouse wheel event
                        break;
                    default:
                        break;
                }
                return flag;
            }
            private static bool MouseClick(uint mouseDown, uint mouseUp, int x, int y)
            {
                bool cusorFlag = SetCursorPos(x, y); // Set the cursor position to the specified coordinates
                if (!cusorFlag) return false;

                //or you do not want use SetcursorPos, you can use this code
                int normalizedX = x * 65535 / Screen.PrimaryScreen.Bounds.Width;
                int normalizedY = y * 65535 / Screen.PrimaryScreen.Bounds.Height;
                //and set 
                inputs[0].u.mi.dx = normalizedX;
                inputs[0].u.mi.dy = normalizedY;

                INPUT[] inputs = new INPUT[2];

                inputs[0].type = INPUT_MOUSE;
                inputs[0].u.mi.dwFlags = mouseDown | MOUSEEVENTF_ABSOLUTE;
                inputs[0].u.mi.dx = 0;
                inputs[0].u.mi.dy = 0;

                inputs[1].type = INPUT_MOUSE;
                inputs[1].u.mi.dwFlags = mouseUp | MOUSEEVENTF_ABSOLUTE;
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


            /// <summary>
            /// this for windows Message
            /// </summary>
            //Left mouse
            const int WM_LBUTTONDBLCLK = 0x0203; //Lefr mouse double click
            const int WM_LBUTTONDOWN = 0x0201; //Left mouse pressed down
            const int WM_LBUTTONUP = 0x0202; // Left mouse released
            const int WM_NCLBUTTONDBLCLK = 0x00A3; //Non-client left mouse double click
            const int WM_NCLBUTTONDOWN = 0x00A1; //Non-client left mouse pressed down
            const int WM_NCLBUTTONUP = 0x00A2; //Non-client left mouse released
            //Middle
            const int WM_MBUTTONDBLCLK = 0x0209; //Middle mouse double click
            const int WM_MBUTTONDOWN = 0x0207; //Middle mouse pressed down
            const int WM_MBUTTONUP = 0x0208; //Middle mouse released
            const int WM_NCMBUTTONDBLCLK = 0x00A9; //Non-client middle mouse double click
            const int WM_NCMBUTTONDOWN = 0x00A7; //Non-client middle mouse pressed down
            const int WM_NCMBUTTONUP = 0x00A8; //Non-client middle mouse released
            const int WM_NCMOUSEHOVER = 0x02A1; //Non-client mouse hover
            const int WM_NCMOUSELEAVE = 0x02A3; //Non-client mouse leave
            const int WM_NCMOUSEMOVE = 0x00A0; //Non-client mouse move
            //Right mouse
            const int WM_RBUTTONDBLCLK = 0x0206; //Right mouse double click
            const int WM_RBUTTONDOWN = 0x0204; //Right mouse pressed down
            const int WM_RBUTTONUP = 0x0205; //Right mouse released
            const int WM_NCRBUTTONDBLCLK = 0x00A6; //Non-client right mouse double click
            const int WM_NCRBUTTONDOWN = 0x00A4; //Non-client right mouse pressed down
            const int WM_NCRBUTTONUP = 0x00A5; //Non-client right mouse released
            //All
            const int WM_MOUSEACTIVATE = 0x0021;
            const int WM_MOUSEHOVER = 0x02A1;
            const int WM_MOUSEHWHEEL = 0x020E;
            const int WM_MOUSELEAVE = 0x02A3;
            const int WM_MOUSEMOVE = 0x0200;
            const int WM_MOUSEWHEEL = 0x020A;
            const int WM_NCHITTEST = 0x0084;
            //mouse event on nonclient(title bar, zoom in, zoom out,...)
            const int WM_NCXBUTTONDBLCLK = 0x00AB; //Non-client X mouse double click
            const int WM_NCXBUTTONDOWN = 0x00A9; //Non-client X mouse pressed down
            const int WM_NCXBUTTONUP = 0x00AA; //Non-client X mouse released

            //X mouse(extra mouse buttons)
            const int WM_XBUTTONDBLCLK = 0x020D; //X mouse double click
            const int WM_XBUTTONDOWN = 0x020B; //X mouse pressed down
            const int WM_XBUTTONUP = 0x020C; //X mouse released

            public static string MouseCoordinate(Message e)
            {
                string result = "";
                int x = e.LParam.ToInt32() & 0xFFFF;
                int y = (e.LParam.ToInt32() >> 16) & 0xFFFF;

                switch (e.Msg)
                {
                    case WM_LBUTTONDOWN:
                        Console.WriteLine($"Mouse left click: x:{x} - y:{y}");
                        result = MouseToString(WM_LBUTTONDOWN, x, y);
                        break;
                    case WM_LBUTTONDBLCLK:
                        result = MouseToString(WM_LBUTTONDBLCLK, x, y);
                        Console.WriteLine($"Mouse left dbclick: x:{x} - y:{y}");
                        break;
                    case WM_MBUTTONDOWN:
                        result = MouseToString(WM_MBUTTONDOWN, x, y);
                        Console.WriteLine($"Mouse middle click: x:{x} - y:{y}");
                        break;
                    case WM_RBUTTONDOWN:
                        result = MouseToString(WM_RBUTTONDOWN, x, y);
                        Console.WriteLine($"Mouse right click: x:{x} - y:{y}");
                        break;
                    case WM_MOUSEWHEEL:
                        result = MouseToString(WM_MOUSEWHEEL, x, y);
                        Console.WriteLine($"Mouse wheel: x:{x} - y:{y}");
                        break;
                    // Non-client area messages (title bar, borders, etc.)
                    case WM_NCLBUTTONDOWN:
                        Console.WriteLine($"NC Mouse left click: x:{x} - y:{y}");
                        result = MouseToString(WM_NCLBUTTONDOWN, x, y);
                        break;
                    case WM_NCLBUTTONDBLCLK:
                        result = MouseToString(WM_NCLBUTTONDBLCLK, x, y);
                        Console.WriteLine($"NC Mouse left dbclick: x:{x} - y:{y}");
                        break;
                    case WM_NCRBUTTONDOWN:
                        result = MouseToString(WM_NCRBUTTONDOWN, x, y);
                        Console.WriteLine($"NC Mouse right click: x:{x} - y:{y}");
                        break;
                    case WM_NCMBUTTONDOWN:
                        result = MouseToString(WM_NCMBUTTONDOWN, x, y);
                        Console.WriteLine($"NC Mouse middle click: x:{x} - y:{y}");
                        break;
                    default:
                        break;
                }
                return result;
            }
            public static string MouseToString(int mouseType, int x, int y)
            {
                return new StringBuilder().Append(mouseType).Append("|").Append(x).Append("|").Append(y).ToString();
            }
        }*/
}
