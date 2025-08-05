using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteClient.Models.Enums;

namespace VRemoteClient.Models.DTOs
{
    public class KeyboardReceived
    {
        public IntPtr Command { get; set; }
        public Keys Modifier { get; set; }
        public Keys Key { get; set; }
        public KeyState Type { get; set; }
    }
}
