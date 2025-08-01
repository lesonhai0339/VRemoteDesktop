using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using static VRemoteClient.Models.Enums.KeyboardEnums;

namespace VRemoteClient.Models.CustomEvents
{
    public class CustomKeyMessageEventArgs: EventArgs
    {
        public CustomKeyMessageEventArgs()
        {
        }
        public CustomKeyMessageEventArgs(IntPtr command, IntPtr handle, Keys keyCode, KeyState keyType, bool isSynthetic = false)
        {
            Command = command;
            Handle = handle;
            KeyCode = keyCode;
            KeyType = keyType;
            IsSynthetic = isSynthetic;
        }
        public CustomKeyMessageEventArgs(IntPtr command, IntPtr handle, Keys keyModifier, Keys keyCode, bool isSynthetic = false)
        {
            Command = command;
            Handle = handle;
            KeyModifier = keyModifier;
            KeyCode = keyCode;
            IsSynthetic = isSynthetic;
        }
        public CustomKeyMessageEventArgs(IntPtr command, IntPtr handle, Keys keyModifier, Keys keyCode, KeyState keyType, bool isSynthetic = false)
        {
            Command = command;
            Handle = handle;
            KeyModifier = keyModifier;
            KeyCode = keyCode;
            KeyType = keyType;
            IsSynthetic = isSynthetic;
        }
        public CustomKeyMessageEventArgs(IntPtr command, IntPtr handle, Keys keyModifier, Keys keyCode, KeyState keyType,Keys modifier2 = Keys.None , bool isSynthetic = false)
        {
            Command = command;
            Handle = handle;
            KeyModifier = keyModifier;
            Keymodifier2 = modifier2;
            KeyCode = keyCode;
            KeyType = keyType;
            IsSynthetic = isSynthetic;
        }
        public IntPtr Command { get; set; }
        public IntPtr Handle { get; set; }
        public Keys KeyModifier { get; set; }
        public Keys Keymodifier2 { get; set; } = Keys.None;
        public Keys KeyCode { get; set; }
        public KeyState KeyType { get; set; }
        public KeyCombination Combination { get; set; } = KeyCombination.None;
        public bool IsSynthetic { get; set; } = false;
    }
}
