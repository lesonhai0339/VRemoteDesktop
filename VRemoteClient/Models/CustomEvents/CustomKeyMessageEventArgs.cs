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
        public CustomKeyMessageEventArgs(IntPtr command, IntPtr handle, Keys keyCode, KeyState keyType)
        {
            Command = command;
            Handle = handle;
            KeyCode = keyCode;
            KeyType = keyType;
        }
        public CustomKeyMessageEventArgs(IntPtr command, IntPtr handle, Keys keyModifier, Keys keyCode)
        {
            Command = command;
            Handle = handle;
            KeyModifier = keyModifier;
            KeyCode = keyCode;
        }
        public CustomKeyMessageEventArgs(IntPtr command, IntPtr handle, Keys keyModifier, Keys keyCode, KeyState keyType)
        {
            Command = command;
            Handle = handle;
            KeyModifier = keyModifier;
            KeyCode = keyCode;
            KeyType = keyType;
        }
        public IntPtr Command { get; set; }
        public IntPtr Handle { get; set; }
        public Keys KeyModifier { get; set; }
        public Keys KeyCode { get; set; }
        public KeyState KeyType { get; set; }
        public KeyCombination Combination { get; set; } = KeyCombination.None;
    }
}
