using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteClient.Models.Enums;

namespace VRemoteClient.Models.CustomEvents
{
    public class KeyMessageEventArgs : EventArgs
    {
        public KeyMessageEventArgs()
        {
        }
        public KeyMessageEventArgs(IntPtr command, Keys keyCode, KeyState keyType)
        {
            Command = command;
            KeyCode = keyCode;
            KeyType = keyType;
        }
        public KeyMessageEventArgs(IntPtr command, Keys keyModifier, Keys keyCode)
        {
            Command = command;
            KeyModifier = keyModifier;
            KeyCode = keyCode;
        }
        public KeyMessageEventArgs(IntPtr command, Keys keyModifier, Keys keyCode, KeyState keyType)
        {
            Command = command;
            KeyModifier = keyModifier;
            KeyCode = keyCode;
            KeyType = keyType;
        }
        public IntPtr Command { get; set; }
        public Keys KeyModifier { get; set; }
        public Keys KeyCode { get; set; }
        public KeyState KeyType { get; set; }
    }
}
