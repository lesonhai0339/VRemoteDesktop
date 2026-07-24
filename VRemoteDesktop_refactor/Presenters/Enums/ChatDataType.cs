using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vsign4.VRemoteDesktop.Presenters.Enums
{
    public enum ChatDataType
    {
        None,
        Connection,
        Message,
        RequestSendFile,
        AcceptSendFile,
        RejectSendFile,
        FileData,
        CancelFileData,
    }
}
