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
