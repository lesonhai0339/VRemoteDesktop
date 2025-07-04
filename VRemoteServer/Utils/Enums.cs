using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VRemoteServer.Utils
{
    public class Enums
    {
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
            Data = 0x04,
            Ping = 0x05,
            Pong = 0x06,
            Error = 0x07,

            LoginFailed = 0x90,
            PartnerDisconnected = 0x91,
            P2PConnectFailed = 0x92,    
        }
    }
}
