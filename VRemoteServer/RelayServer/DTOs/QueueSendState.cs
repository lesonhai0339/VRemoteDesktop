using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VRemoteServer.RelayServer.DTOs
{
    public class QueueSendState
    {
        public Queue<(byte[] data, int length, bool isPooled)> Queue = new Queue<(byte[] data, int length, bool isPooled)>();
        public bool IsSending = false;
        public object LockSend = new object();
    }
}
