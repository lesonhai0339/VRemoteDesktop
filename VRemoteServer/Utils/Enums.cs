using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VRemoteServer.Utils
{
    public class Enums
    {
        public enum CommandType: byte
        {
            None = 0x00,
            Login = 0x01,
            Connect = 0x02,
            Disconnect = 0x03,
            Data = 0x04,
            Ping = 0x05,
            Pong = 0x06,
            Error = 0x07
        }
    }
}
