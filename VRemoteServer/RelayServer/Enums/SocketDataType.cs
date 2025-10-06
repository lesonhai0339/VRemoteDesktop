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
        P2PRequestToConnect = 0x10, //RequestToConnectP2P
        P2PRespondRequestToConnect = 0x11, //RespondRequestToConnectP2P
        P2PAcceptConnect = 0x12, //AcceptConnectP2P 
        P2PLogin = 0x16, //DataTransferP2P
        P2PLoginSucceed = 0x17, //DataTransferP2PSucceed
        P2PLoginFailed = 0x18, //DataTransferP2PFailed

        //Remote control
        RemoteControlScreenSend = 0x07, //Screen
        RemoteControlScreenRegionsChangedSend = 0x08, //Chunks
        RemoteControlKeyboardSend = 0x09, //Keyboard
        RemoteControlMouseSend = 0x0A, //Mouse
        RemoteControlClipboardSend = 0x0E, //Clipboard
        RemoteControlChatSend = 0x0F, //Chat
        RemoteControlRespondScreenSend = 0x0C, //ScreenOk
        RemoteControlRespondScreenRegionsChangedSend = 0x0D, //ChunksOk

        RemoteControlRequestToConnect = 0x02,
        RemoteControlDataSend = 0x14, //P2PDataSend
        RemoteControlDataSendFailed = 0x15, //P2PDataSendError
        RemoteControlDisconnect = 0x91, //P2PDisconnect
        RemoteControlConnectFailed = 0x92, //P2PConnectFailed
        RemoteControlAcceptedRequestToConnect = 0x93, //P2PAcceptConnect
        RemoteControlRefusedRequestToConnect = 0x94, //P2PRejectConnect
        RemoteControlReady = 0x95,
    }
}
