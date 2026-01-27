using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VRemoteServer.RelayServer.Enums
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

        P2PConnect = 0x10, //RequestToConnectP2P
        P2PDataRespond = 0x11, //RespondRequestToConnectP2P
        P2PAcceptConnect = 0x12, //AcceptConnectP2P 
        P2PLogin = 0x16, //DataTransferP2P
        P2PLoginSucceed = 0x17, //DataTransferP2PSucceed
        P2PLoginFailed = 0x18, //DataTransferP2PFailed
        P2PInvalidConnectData = 0x19,

        //Remote control
        ScreenSend = 0x07, //Screen
        ScreenRegionsChangedSend = 0x08, //Chunks
        RemoteControlScreenSend = 0x09, //Keyboard
        MouseSend = 0x0A, //Mouse
        ClipboardSend = 0x0E, //Clipboard
        ChatSend = 0x0F, //Chat
        ScreenOk = 0x0C, //ScreenOk
        RegionsChangedOk = 0x0D, //ChunksOk
        Ready = 0x95,

        RemoteControlRequestToConnect = 0x02,
        RemoteControlDataSend = 0x14, //P2PDataSend
        RemoteControlDataSendFailed = 0x15, //P2PDataSendError
        RemoteControlDisconnect = 0x91, //P2PDisconnect
        RemoteControlConnectFailed = 0x92, //P2PConnectFailed
        RemoteControlAcceptedRequestToConnect = 0x93, //P2PAcceptConnect
        RemoteControlRefusedRequestToConnect = 0x94, //P2PRejectConnect



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
