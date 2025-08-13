using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Enums
{
    public enum SendFileType
    {
        None = 0,
        RequestSendFile = 1,
        AcceptSendFile = 2,
        FileTransfer = 3
    }
}
