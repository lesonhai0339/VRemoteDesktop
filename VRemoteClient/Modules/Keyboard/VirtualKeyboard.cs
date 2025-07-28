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

namespace VRemoteClient.Modules.Keyboard
{
    public class KeyboardState
    {
        public Keys Key { get; set; }
        public DateTime PressTime { get; set; } = DateTime.Now;
    }
    public static class VirtualKeyboard
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
                        wScan = 0,
                        dwFlags = keyState == KeyState.KeyDown ? 0 : KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = GetMessageExtraInfo()
                    }
                }
            };
            uint status = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
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
                        dwExtraInfo = GetMessageExtraInfo()
                    }
                }
            };

            uint status = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));

            return status > 0;
        }

        //alternative methods
        /*  private static ConcurrentQueue<KeyboardObject> KeyStorage = new ConcurrentQueue<KeyboardObject>();
          private static readonly HashSet<Keys> ModifierKeys = new HashSet<Keys>()
          {
              Keys.Control, Keys.ControlKey, Keys.LControlKey, Keys.RControlKey,
              Keys.Shift, Keys.ShiftKey, Keys.LShiftKey, Keys.RShiftKey,
              Keys.Alt, Keys.Menu, Keys.LMenu, Keys.RMenu, Keys.LWin, Keys.RWin, Keys.Apps
          };
          private static List<Keys> _modifiers = new List<Keys>();
          private static Keys _key = Keys.None;

          private static object _lock = new object();
          private static KeyboardObject _keyObject = null;
          public static uint SendKey(Keys key)
          {
              if (isDisposed) return 0;

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

  */
        /*        public static void ProcessKeyboardReceived(Keys key, KeyState keyState)
          {
              if (isDisposed) return;

              if (keyState == KeyState.KeyDown)
              {
                  KeyDownEvent(key);
              }
              else if (keyState == KeyState.KeyUp)
              {
                  KeyUpEvent(key);
              }
              else { }
          }
          private static void KeyDownEvent(Keys key)
          {
              bool isModifier = ModifierKeys.Contains(key);
              lock (_lock)
              {
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
                          if (_keyObject.Key == Keys.None)
                          {
                              _keyObject.Key = key;
                          }
                          else if (_keyObject.Key == key)
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
          }
          private static void KeyUpEvent(Keys key)
          {
              lock (_lock)
              {
                  if (_keyObject == null) return;

                  bool isModifier = ModifierKeys.Contains(key);

                  if (isModifier)
                  {
                      if (_keyObject.Modifiers.Contains(key))
                      {
                          _keyObject.ModifiersReleased += 1;
                          //missing handle single modifier keyboard like Lwin(down) and Lwin(up)
                      }
                  }
                  else if (_keyObject.Key == key)
                  {
                      _keyObject.IsKeyUp = true;
                  }

                  if (_keyObject.IsKeyUp && (_keyObject.ModifiersReleased == _keyObject.Modifiers.Count))
                  {
                      KeyStorage.Enqueue(_keyObject);
                      _keyObject = null;
                  }
              }
          }*//*
          private static void KeyEventHandler()
          {
              while (KeyStorage.TryDequeue(out var key))
              {
                  if (key.Key != Keys.None)
                  {
                      if (key.Modifiers.Count > 1)
                      {
                          SendKeyWithModifiers(key.Modifiers, key.Key);
                      }
                      else if (key.Modifiers.Count == 1)
                      {
                          SendKeyWithModifier(key.Modifiers[0], key.Key);
                      }
                      else
                      {
                          SendKey(key.Key);
                      }
                  }
                  else if (key.Modifiers.Count == 1)
                  {
                      SendKey(key.Modifiers[0]);
                  }
              }
          }
          public static uint SendKeyWithModifiers(List<Keys> modifiers, Keys key)
          {
              if (isDisposed || modifiers == null || !modifiers.Any())
                  return SendKey(key);

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
          public static uint SendKeyWithModifier(Keys modifier, Keys key)
          {
              if (isDisposed) return 0;

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
          }*/
    }
}
