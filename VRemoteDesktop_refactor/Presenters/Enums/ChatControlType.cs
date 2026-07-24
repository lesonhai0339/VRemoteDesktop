using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vsign4.VRemoteDesktop.Presenters.Enums
{
    public enum ChatControlType
    {
        Connection,
        Message,
        RequestAttachment,
        ReceivedAttachment,
        AcceptAttachment,
        RefuseAttachment,
        StopSendingAttachment
    }
}
