using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Enums
{
    public enum SendFileRespondType:  byte
    {
        Accept = 0x01,
        Reject = 0x02
    }
}
