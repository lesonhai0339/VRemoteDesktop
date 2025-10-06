using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace VRemoteDesktop.Models
{
    public class KeyboardState
    {
        public Keys Key { get; set; }
        public DateTime PressTime { get; set; } = DateTime.Now;
    }
}
