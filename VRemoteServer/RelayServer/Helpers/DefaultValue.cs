using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VRemoteServer.RelayServer.Helpers
{
    public static class DefaultValue
    {
        public static class Common
        {
            public static char SEPARATOR = '|';
            public static int DEFAULT_PORT = 2399;
        }
        public static class SocketConnectionDefault
        {
            public static int TIMEOUT = 30; //Unit: second
            public static int PACKET_HEADER_LENGTH = 13;
            public static int PACKET_SIZE_LENGTH = 4;
            public static int PACKET_SIZE_INDEX = 0;
            public static int PACKET_TYPE_LENGTH = 1;
            public static int PACKET_TYPE_INDEX = 4;
            public static int PACKET_ID_LENGTH = 8;
            public static int PACKET_ID_INDEX = 5;
            public static string EMPTY_ID = "00000000";
        }
    }
}
