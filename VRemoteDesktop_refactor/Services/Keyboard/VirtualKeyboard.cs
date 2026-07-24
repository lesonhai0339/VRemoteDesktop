using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Vsign4.VRemoteDesktop.Services.Keyboard.DTOs;
using Vsign4.VRemoteDesktop.Services.Keyboard.Enums;

namespace Vsign4.VRemoteDesktop.Services.Keyboard
{
    internal static class VirtualKeyboard
    {
        private static ConcurrentDictionary<Keys, KeyboardState> _pressedKeys = new ConcurrentDictionary<Keys, KeyboardState>();
        private static System.Threading.Timer processingTimer;

        private static volatile bool isDisposed = false;
        private static volatile bool isProcessing = false;

        private const int KEYBOARD_MIN_FIELDS = 4;
        private const int KEYBOARD_COMMAND_INDEX = 0;
        private const int KEYBOARD_MODIFIER_INDEX = 1;
        private const int KEYBOARD_KEY_INDEX = 2;
        private const int KEYBOARD_TYPE_INDEX = 3;
        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;


        static VirtualKeyboard()
        {
            Initialize();
        }
        private static void Initialize()
        {
            processingTimer = new System.Threading.Timer(ProcessQueue, null, 0, 50);

            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;
        }
        /// <summary>
        /// Parse byte array to KeyboardReceived object
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static KeyboardReceived BytesToCustomKeyboardEvent(byte[] data)
        {
            if (data == null || data.Length == 0)
                return null;

            int command;
            int modifier;
            int key;
            int type;
            string[] keyboards = Encoding.ASCII.GetString(data).Trim().Split('|');
            if (keyboards.Length != KEYBOARD_MIN_FIELDS)
                return null;
            if (!int.TryParse(keyboards[KEYBOARD_COMMAND_INDEX], out command))
                return null;
            if (!int.TryParse(keyboards[KEYBOARD_MODIFIER_INDEX], out modifier)
                    || !Enum.IsDefined(typeof(Keys), modifier))
                return null;
            if (!int.TryParse(keyboards[KEYBOARD_KEY_INDEX], out key)
                    || !Enum.IsDefined(typeof(Keys), key))
                return null;
            if (!int.TryParse(keyboards[KEYBOARD_TYPE_INDEX], out type)
                   || !Enum.IsDefined(typeof(KeyState), type))
                return null;

            return new KeyboardReceived
            {
                Command = (IntPtr)command,
                Modifier = (Keys)modifier,
                Key = (Keys)key,
                Type = (KeyState)type,
            };
        }
        private static void OnProcessExit(object sender, EventArgs e)
        {
            Dispose();
        }
        private static void OnDomainUnload(object sender, EventArgs e)
        {
            Dispose();
        }
        public static void Dispose()
        {
            if (isDisposed) return;

            isDisposed = true;

            if(processingTimer != null)
                processingTimer.Dispose();

            //while (KeyStorage.TryDequeue(out _)) { } ;
            //lock (_lock)
            //{
            //    _keyObject = null;
            //}
        }
        private static void ProcessQueue(object state)
        {
            if (isProcessing) return;
            isProcessing = true;

            try
            {
                var now = DateTime.Now;
                var timeoutKeys = _pressedKeys.Where(kvp =>
                    (now - kvp.Value.PressTime).TotalMilliseconds > 500).ToList();

                foreach (var timeoutKey in timeoutKeys)
                {

                    bool flag = ReleaseKey(timeoutKey.Key);
                    if (flag)
                    {
                        KeyboardState s;
                        _pressedKeys.TryRemove(timeoutKey.Key, out s);
                    }
                }
                //KeyEventHandler(); // Process queue
            }
            finally
            {
                isProcessing = false;
            }
        }
        public static void ProcessKeyboardReceived(Keys key, KeyState keyState)
        {
            Interop.Win32Apis.INPUT[] inputs = new Interop.Win32Apis.INPUT[1];
            inputs[0] = new Interop.Win32Apis.INPUT
            {
                type = INPUT_KEYBOARD,
                u = new Interop.Win32Apis.InputUnion
                {
                    ki = new Interop.Win32Apis.KEYBDINPUT
                    {
                        wVk = (ushort)key,
                        wScan = (ushort)Interop.Win32Apis.KeyboardApis.MapVirtualKeyA((uint)key, 0), //windows using scan code wScan 
                                                                                   //wScan = 0,  //windows using virtual key code wVk 
                        dwFlags = keyState == KeyState.KeyDown ? 0 : KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = new IntPtr(Interop.Win32Apis.SYNTHETIC_KEY_MARKER) //Magic number, used to classify key pressed from USB or from virtual keyboard
                    }
                }
            };
            uint status = Interop.Win32Apis.WindowApis.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Interop.Win32Apis.INPUT)));
            if (status > 0)
            {
                if (keyState == KeyState.KeyDown)
                {
                    _pressedKeys.AddOrUpdate(key,
                        new KeyboardState { Key = key },
                        (k, v) => new KeyboardState { Key = key });
                }
                else if (keyState == KeyState.KeyUp)
                {
                    KeyboardState s;    
                    _pressedKeys.TryRemove(key, out s);
                }
            }
        }
        private static bool ReleaseKey(Keys key)
        {
            Interop.Win32Apis.INPUT[] inputs = new Interop.Win32Apis.INPUT[1];
            inputs[0] = new Interop.Win32Apis.INPUT
            {
                type = INPUT_KEYBOARD,
                u = new Interop.Win32Apis.InputUnion
                {
                    ki = new Interop.Win32Apis.KEYBDINPUT
                    {
                        wVk = (ushort)key,
                        wScan = 0,
                        dwFlags = KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = Interop.Win32Apis.WindowApis.GetMessageExtraInfo()
                    }
                }
            };

            uint status = Interop.Win32Apis.WindowApis.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Interop.Win32Apis.INPUT)));

            return status > 0;
        }
    }
}
