using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vsign4.VRemoteDesktop.Services.SessionManagement.Enums
{
    public enum SocketDataType : byte
    {
        //Login
        None = 0x00,
        Login = 0x01,
        Connect = 0x13,
        LoginFailed = 0x90,
        Disconnect = 0x03,
        Ping = 0x04,
        Pong = 0x05,
        Error = 0x06,

        P2PConnect = 0x10,
        P2PDataRespond = 0x11,
        P2PAcceptConnect = 0x12,
        P2PLogin = 0x16,
        P2PLoginRespond = 0x17,
        P2PInvalidConnectData = 0x19,

        //Remote control
        ScreenSend = 0x07,
        ScreenRegionsChangedSend = 0x08,
        KeyboardSend = 0x09,
        MouseSend = 0x0A,
        ClipboardSend = 0x0E,
        ChatSend = 0x0F,
        ScreenOk = 0x0C,
        RegionsChangedOk = 0x0D,
        Ready = 0x95,

        RemoteControlRequestToConnect = 0x02,
        RemoteControlDataSend = 0x14, //P2PDataSend
        RemoteControlDataSendFailed = 0x15, //P2PDataSendError
        RemoteControlDisconnect = 0x91, //P2PDisconnect
        RemoteControlConnectFailed = 0x92, //P2PConnectFailed
        RemoteControlAcceptedRequestToConnect = 0x93, //P2PAcceptConnect
        RemoteControlRefusedRequestToConnect = 0x94, //P2PRejectConnect


        //New
        GetPartnerInfo = 200,
        GetPartnerInfoSuccess = 201,
        GetPartnerInfoFailed = 202,
        RequestRemoteConnect = 203,
        LoginResponse = 204,
        RemoteLogin = 205,
        RemoteLoginSuccess = 206,
        RemoteLoginFailed = 207,
        ReadyToRemoteController = 208,
        ReadyToRemoteControlled = 209,
        P2PReady = 210

    }
}
