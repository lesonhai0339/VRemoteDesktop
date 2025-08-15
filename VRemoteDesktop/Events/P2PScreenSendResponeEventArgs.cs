using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Enums;

namespace VRemoteDesktop.Events
{
    public class P2PScreenSendResponeEventArgs: EventArgs
    {
        public P2PScreenSendResponeEventArgs(ScreenType type, bool isSuccess)
        {
            Type = type;
            IsSuccess = isSuccess;
        }

        public ScreenType Type { get; set; }
        public bool IsSuccess { get; set; }
    }
}
