using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.Enums;

namespace VRemoteDesktop.Events
{
    public class KeyEventArgs : EventArgs
    {
        public KeyEventArgs()
        {
        }
        public KeyEventArgs(IntPtr command, IntPtr handle, Keys keyCode, KeyState keyType, bool isSynthetic = false)
        {
            Command = command;
            Handle = handle;
            KeyCode = keyCode;
            KeyType = keyType;
            IsSynthetic = isSynthetic;
        }
        public KeyEventArgs(IntPtr command, IntPtr handle, Keys keyModifier, Keys keyCode, bool isSynthetic = false)
        {
            Command = command;
            Handle = handle;
            KeyModifier = keyModifier;
            KeyCode = keyCode;
            IsSynthetic = isSynthetic;
        }
        public KeyEventArgs(IntPtr command, IntPtr handle, Keys keyModifier, Keys keyCode, KeyState keyType, bool isSynthetic = false)
        {
            Command = command;
            Handle = handle;
            KeyModifier = keyModifier;
            KeyCode = keyCode;
            KeyType = keyType;
            IsSynthetic = isSynthetic;
        }
        public IntPtr Command { get; set; }
        public IntPtr Handle { get; set; }
        public Keys KeyModifier { get; set; }
        public Keys KeyCode { get; set; }
        public KeyState KeyType { get; set; }
        public KeyCombination Combination { get; set; } = KeyCombination.None;
        public bool IsSynthetic { get; set; } = false;
    }
}
