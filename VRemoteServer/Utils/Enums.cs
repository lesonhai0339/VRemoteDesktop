using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VRemoteServer.Utils
{
    public class Enums
    {
        public enum Connecter: int
        {
            Sender = 0,
            Receiver = 1
        }
        public enum Test : int
        {
            PING= 0,
            LOGIN =1,
            P2PCONNECT = 2,
            P2PDATASEND = 3,
        }
        public enum CommandType: byte
        {
            None = 0x00,
            Login = 0x01,
            P2PConnect = 0x02,
            Disconnect = 0x03,
            Ping = 0x04,
            Pong = 0x05,
            Error = 0x06,
            Screen = 0x07,
            Chunks = 0x08,
            Keyboard = 0x09,
            Mouse = 0x0A,
            ScreenOk = 0x0C,
            ChunksOk = 0x0D,
            Clipboard = 0x0E,
            Message = 0x0F,
            FileTransfer = 0x10,
            RequestSendFile = 0x11,
            AcceptSendFile = 0x12,

            LoginFailed = 0x90,
            P2PDisconnect = 0x91,
            P2PConnectFailed = 0x92,
        }
    }
}
