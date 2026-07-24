using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Vsign4.VRemoteDesktop.Services.Keyboard.DTOs
{
    public class KeyboardState
    {
        public Keys Key { get; set; }
        public DateTime PressTime { get; set; }
    }
}
