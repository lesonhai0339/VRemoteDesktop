using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Models;
using static VRemoteDesktop.Interop.Win32Apis;
using static VRemoteDesktop.Utils.Logger;
using VRemoteDesktop.Utils;

namespace VRemoteDesktop.Services.Keyboard
{
    internal static class VirtualKeyboard
    {
        private static ConcurrentDictionary<Keys, KeyboardState> _pressedKeys = new ConcurrentDictionary<Keys, KeyboardState>();
        private static System.Threading.Timer processingTimer;

        private static volatile bool isDisposed = false;
        private static volatile bool isProcessing = false;
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
        public static KeyboardReceived BytesToCustomKeyboardEvent(byte[] data)
        {
            if (data == null || data.Length == 0)
                return null;

            string[] keyboards = Encoding.ASCII.GetString(data).Trim().Split('|');

            if (keyboards.Length != DefaultKeyboard.KEYBOARD_MIN_FIELDS)
                return null;
            if (!int.TryParse(keyboards[DefaultKeyboard.KEYBOARD_COMMAND_INDEX], out int command))
                return null;
            if (!int.TryParse(keyboards[DefaultKeyboard.KEYBOARD_MODIFIER_INDEX], out int modifier)
                    || !Enum.IsDefined(typeof(Keys), modifier))
                return null;
            if (!int.TryParse(keyboards[DefaultKeyboard.KEYBOARD_KEY_INDEX], out int key)
                    || !Enum.IsDefined(typeof(Keys), key))
                return null;
            if (!int.TryParse(keyboards[DefaultKeyboard.KEYBOARD_TYPE_INDEX], out int type)
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
        private static void OnProcessExit(object sender, EventArgs e) => Dispose();
        private static void OnDomainUnload(object sender, EventArgs e) => Dispose();
        public static void Dispose()
        {
            if (isDisposed) return;

            isDisposed = true;
            processingTimer?.Dispose();

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
                        _pressedKeys.TryRemove(timeoutKey.Key, out _);
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
            INPUT[] inputs = new INPUT[1];
            inputs[0] = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = (ushort)key,
                        wScan = (ushort)KeyboardApis.MapVirtualKeyA((uint)key, 0), //windows using scan code wScan 
                        //wScan = 0,  //windows using virtual key code wVk 
                        dwFlags = keyState == KeyState.KeyDown ? 0 : KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = new IntPtr(SYNTHETIC_KEY_MARKER) //Magic number, used to classify key pressed from USB or from virtual keyboard
                    }
                }
            };
            uint status = WindowApis.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
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
                    _pressedKeys.TryRemove(key, out _);
                }
            }
        }
        private static bool ReleaseKey(Keys key)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0] = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = (ushort)key,
                        wScan = 0,
                        dwFlags = KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = WindowApis.GetMessageExtraInfo()
                    }
                }
            };

            uint status = WindowApis.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));

            return status > 0;
        }
    }
}
