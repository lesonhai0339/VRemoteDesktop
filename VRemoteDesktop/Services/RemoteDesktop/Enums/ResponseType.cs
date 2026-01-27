using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Services.RemoteDesktop.Enums
{
    public enum ResponseType
    {
        ConnectSuccess,
        ConnectFailed,
        LoginSuccess,
        LoginFailed,
        Disconnect,
        GetPartnerInfoFailed,
        GetPartnerInfoSuccess,  
        AddRemoteController,
        AddRemoteControlled,
        RemoteDisconnect
    }
}
