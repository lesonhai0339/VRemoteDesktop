using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Enums
{
    public enum MouseEventType: byte
    {
        None = 0,
        Click = 1,
        DoubleClick = 2,
        TripleClick = 3,
        Wheel = 4,
        DragAndDrop = 5,
        Move = 6,
    }
}
