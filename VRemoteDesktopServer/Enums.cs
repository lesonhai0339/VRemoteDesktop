using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktopServer
{
    public class Enums
    {
        public enum RemoteType : int
        {
            OWNER = 0,
            PARTNER = 1
        }
        public enum SendType: int
        {
            INIT_CONNECTION = 0 ,
            PING = 1,
            SHARESCREEN = 2,
            SENDKEY = 3,
            SENDMOUSE = 4,
            SENDTEXT = 5,
            SENDSHORTCUT = 6,
            SENDFILE = 7,
        }
    }
}
