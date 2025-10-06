using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.Enums;

namespace VRemoteDesktop.Models
{
    public class MouseData
    {
        public MouseData(VMouseButtons button, int clicks, int x, int y, int delta)
        {
            Button = button;
            Clicks = clicks;
            X = x;
            Y = y;
            Delta = delta;
        }

        public  VMouseButtons Button { get; set; }

        public int Clicks { get; set; }

        public int X {  get; set; }

        public int Y {  get; set; }

        public int Delta { get; set; }
    }
}
