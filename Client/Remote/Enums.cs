using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemoteClient.Remote
{
    public class Enums
    {
        public enum DataType: int
        {
            PING = 0,
            LOGIN = 1,
            P2PCONNECT= 2,
            P2PDATASEND = 3
        }
    }
}
