using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vinhhy_Remote_Desktop_Server
{
    internal class Enums
    {
        
    }
    public enum RemoteType : int
    {
        OWNER = 0,
        PARTNER = 1
    }
    public enum DataSendType : int
    {
        INIT = 1,
        KEYBOARD = 2,
        SCREEN = 3,
        CHUNK = 4,
        FILE = 5,
        CHAT = 6,
        CONTROL = 7,
    }
    public enum ConnectType : int
    {
        OWNER = 0,
        PARTNER = 1,
    }
    public enum SocketResponseType : int
    {
        NONE = 0,
        SCREEN = 1,
        KEYBOARD = 2,
        MOUSE = 3,
        FILE = 4,
        FILE_RESPONSE = 5,
        FILE_REQUEST = 6,
        FILE_LIST = 7,
        FILE_LIST_RESPONSE = 8,
        FILE_LIST_REQUEST = 9,
        FILE_LIST_ITEM = 10,
    }
}
