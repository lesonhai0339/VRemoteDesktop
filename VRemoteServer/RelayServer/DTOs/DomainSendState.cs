using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VRemoteServer.RelayServer.DTOs
{
    public class DomainSendState
    {
        public Queue<byte[]> Queue = new Queue<byte[]>();
        public bool IsSending = false;
        public object SendLock = new object();
        public int Offset = 0;
        public int Length = 0;
    }
}
