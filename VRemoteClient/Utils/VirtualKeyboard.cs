using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using VRemoteClient.Models.Entities;
using static VRemoteClient.Models.Enums.KeyboardEnums;
using static VRemoteClient.Utils.Libraries;

namespace VRemoteClient.Utils
{
    public static class VirtualKeyboard
    {
        private static ConcurrentQueue<KeyboardObject> KeyStorage = new ConcurrentQueue<KeyboardObject>();
        private static List<Keys> ModifierKeys = new List<Keys>()
        {
            Keys.Control, Keys.ControlKey, Keys.LControlKey, Keys.RControlKey, Keys.Shift, Keys.ShiftKey, Keys.LShiftKey,
            Keys.RShiftKey, Keys.Alt, Keys.Menu, Keys.LMenu, Keys.RMenu, Keys.LWin, Keys.RWin, Keys.Apps
        };

        private static object _lock = new object();
        private static ConcurrentBag<KeyboardObject> keyboardObjects = new ConcurrentBag<KeyboardObject>();
        private static KeyboardObject _keyObject = null;
        private static List<Keys> _modifiers = new List<Keys>();
        private static Keys _key = Keys.None;
        private const int KEYEVENTF_EXTENDEDKEY = 0x0001; // Extended key flag
        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        public static uint SendKey(Keys key)
        {
            INPUT[] inputs = new INPUT[2];

            // Key down
            inputs[0] = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = (ushort)key,
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = GetMessageExtraInfo()
                    }
                }
            };

            // Key up
            inputs[1] = new INPUT
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
                        dwExtraInfo = GetMessageExtraInfo()
                    }
                }
            };

            uint status = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            return status;
        }
        private static ushort GetKeyValue(Keys key)
        {
            switch (key)
            {
                // Control keys
                case Keys.Control:
                case Keys.LControlKey:
                case Keys.RControlKey:
                    return 0x11;

                // Alt keys  
                case Keys.Alt:
                case Keys.LMenu:
                case Keys.RMenu:
                    return 0x12;

                // Shift keys
                case Keys.Shift:
                case Keys.LShiftKey:
                case Keys.RShiftKey:
                    return 0x10;

                // Windows keys
                case Keys.LWin:
                    return 0x5B;
                case Keys.RWin:
                    return 0x5C;

                // All other keys
                default:
                    return (ushort)key;
            }
        }

        public static void ProcessKeyboardReceived(Keys key, KeyState keyState)
        {
            if(keyState == KeyState.KeyDown)
            {
                KeyDownEvent(key);
            }
            else if(keyState == KeyState.KeyUp)
            {
                KeyUpEvent(key);
            }
            else { }
        }
        private static void KeyDownEvent(Keys key)
        {
            bool isModifier = ModifierKeys.Contains(key);
            if (_keyObject == null)
            {
                _keyObject = new KeyboardObject();
                if (isModifier)
                {
                    _keyObject.Modifiers.Add(key);
                }
                else
                {
                    _keyObject.Key = key;
                }
            }
            else
            {
                if (isModifier)
                {
                    if (!_keyObject.Modifiers.Contains(key))
                    {
                        _keyObject.Modifiers.Add(key);
                    }
                }
                else
                {
                    if(_keyObject.Key == Keys.None)
                    {
                        _keyObject.Key = key;
                    }
                    else if(_keyObject.Key == key)
                    {
                        KeyStorage.Enqueue(new KeyboardObject { Key = key, IsKeyUp = false });
                    }
                    else
                    {
                        KeyStorage.Enqueue(new KeyboardObject
                        {
                            Key = _keyObject.Key,
                            Modifiers = new List<Keys>(_keyObject.Modifiers),
                            IsKeyUp = false
                        });

                        _keyObject = new KeyboardObject
                        {
                            Key = key,
                            Modifiers = new List<Keys>(_keyObject.Modifiers)
                        };
                    }
                }
            }
            
        }
        private static void KeyUpEvent(Keys key)
        {
            if (_keyObject == null) return;

            bool isModifier = ModifierKeys.Contains(key);

            if (isModifier)
            {
                if (_keyObject.Modifiers.Contains(key))
                {
                    _keyObject.ModifiersReleased += 1;
                }
            }
            else if(_keyObject.Key == key)
            {
                _keyObject.IsKeyUp = true;
            }

            if(_keyObject.IsKeyUp && (_keyObject.ModifiersReleased == _keyObject.Modifiers.Count))
            {
                KeyStorage.Enqueue(_keyObject);
                _keyObject = null;
            }
        }
        private static void Method2()
        {
            while(KeyStorage.TryDequeue(out var key))
            {
                if (key.Key != Keys.None)
                {
                    // Có key chính
                    if (key.Modifiers.Count > 1)
                    {
                        SendMultiCombo(key.Modifiers, key.Key);
                    }
                    else if (key.Modifiers.Count == 1)
                    {
                        SendKeyCombo(key.Modifiers[0], key.Key);
                    }
                    else
                    {
                        SendKey(key.Key);
                    }
                }
                else if (key.Modifiers.Count == 1)
                {
                    // Edge case: chỉ có modifier
                    SendKey(key.Modifiers[0]);
                }
            }
        }
        public static void Method_1(Keys key, KeyState state)
        {
            if (state == KeyState.KeyDown)
            {
                switch (key)
                {
                    case Keys.Control:
                    case Keys.ControlKey:
                    case Keys.LControlKey:
                    case Keys.RControlKey:
                    case Keys.Shift:
                    case Keys.ShiftKey:
                    case Keys.LShiftKey:
                    case Keys.RShiftKey:
                    case Keys.Alt:
                    case Keys.Menu:
                    case Keys.LMenu:
                    case Keys.RMenu:
                    case Keys.LWin:
                    case Keys.RWin:
                        lock (_lock)
                        {
                            if (!_modifiers.Any(x => x == key))
                                _modifiers.Add(key);
                        }
                        break;
                    default:
                        lock (_lock)
                        {
                            //case for holding key (example: aaaaaaaaaaaaa)
                            if (_key == key)
                            {
                                SendKey(_key);
                            }
                            else
                            {
                                _key = key;
                            }
                        }
                        break;
                }
                return;
            }
            else
            {
                lock (_lock)
                {
                    // key press event
                    string a = string.Join(" - ", _modifiers);
                    Console.WriteLine(a + " - " + _key);
                    if (_modifiers.Count > 1 && _key != Keys.None)
                    {
                        SendMultiCombo(_modifiers, _key);
                    }
                    else if (_modifiers.Count == 1 && _key != Keys.None)
                    {
                        SendKeyCombo(_modifiers[0], _key);
                    }
                    else if (_modifiers.Count == 1)
                    {
                        SendKey(_modifiers[0]);
                    }
                    else
                    {
                        SendKey(_key);
                    }

                    // remove
                    //_key = Keys.None;
                    //_modifiers.RemoveAll(k => k == key);

                    if (_key == key)
                    {
                        _key = Keys.None;
                    }
                    if (_modifiers.Contains(key))
                    {
                        _modifiers.RemoveAll(k => k == key);
                    }
                }
            }
        }
        public static uint SendMultiCombo(List<Keys> modifiers, Keys key)
        {
            int inputCount = modifiers.Count * 2 + 2; // down/up for each modifier + down/up for main key
            INPUT[] inputs = new INPUT[inputCount];
            int index = 0;

            // 1. Modifier key down
            foreach (var mod in modifiers)
            {
                ushort modVK = GetKeyValue(mod);
                inputs[index++] = CreateKeyInput(modVK, 0); // key down
            }

            // 2. Main key down
            ushort keyVK = GetKeyValue(key);
            inputs[index++] = CreateKeyInput(keyVK, 0); // key down

            // 3. Main key up
            inputs[index++] = CreateKeyInput(keyVK, KEYEVENTF_KEYUP); // key up

            // 4. Modifier key up (in reverse order)
            for (int i = modifiers.Count - 1; i >= 0; i--)
            {
                ushort modVK = GetKeyValue(modifiers[i]);
                inputs[index++] = CreateKeyInput(modVK, KEYEVENTF_KEYUP); // key up
            }

            // 5. Send inputs
            return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }
        public static uint SendKeyCombo(Keys modifier, Keys key)
        {
            INPUT[] inputs = new INPUT[4];

            ushort keyVK = GetKeyValue(key);
            ushort modifierVK = GetKeyValue(modifier);

            // Modifier down
            inputs[0] = CreateKeyInput(modifierVK, 0);
            // Key down
            inputs[1] = CreateKeyInput(keyVK, 0);
            // Key up
            inputs[2] = CreateKeyInput(keyVK, KEYEVENTF_KEYUP);
            // Modifier up
            inputs[3] = CreateKeyInput(modifierVK, KEYEVENTF_KEYUP);

            uint status = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            return status;
        }
        private static INPUT CreateKeyInput(ushort key, uint flags)
        {
            return new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = key,
                        wScan = 0,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = GetMessageExtraInfo()
                    }
                }
            };
        }
    }
}
