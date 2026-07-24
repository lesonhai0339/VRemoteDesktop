using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Vsign4.VRemoteDesktop.Services.Keyboard.Enums;

namespace Vsign4.VRemoteDesktop.Events
{
    public class KeyboardEventArgs : EventArgs
    {
        public KeyboardEventArgs()
        {
            Command = IntPtr.Zero;
            Handle = IntPtr.Zero;
            KeyModifier = Keys.None;
            KeyCode = Keys.None;
            KeyType = KeyState.None;
            Combination = KeyCombination.None;
            IsSynthetic = false;
        }
        public KeyboardEventArgs(IntPtr command, IntPtr handle, Keys keyCode, KeyState keyType, bool isSynthetic = false)
        {
            Command = command;
            Handle = handle;
            KeyCode = keyCode;
            KeyType = keyType;
            IsSynthetic = isSynthetic;
            KeyModifier = Keys.None;
            Combination = KeyCombination.None;
        }
        public KeyboardEventArgs(IntPtr command, IntPtr handle, Keys keyModifier, Keys keyCode, bool isSynthetic = false)
        {
            Command = command;
            Handle = handle;
            KeyModifier = keyModifier;
            KeyCode = keyCode;
            IsSynthetic = isSynthetic;
        }
        public KeyboardEventArgs(IntPtr command, IntPtr handle, Keys keyModifier, Keys keyCode, KeyState keyType, bool isSynthetic = false)
        {
            Command = command;
            Handle = handle;
            KeyModifier = keyModifier;
            KeyCode = keyCode;
            KeyType = keyType;
            Combination = KeyCombination.None;
            IsSynthetic = isSynthetic;
        }
        public IntPtr Command { get; set; }
        public IntPtr Handle { get; set; }
        public Keys KeyModifier { get; set; }
        public Keys KeyCode { get; set; }
        public KeyState KeyType { get; set; }
        public KeyCombination Combination { get; set; }
        public bool IsSynthetic { get; set; }
    }
}
