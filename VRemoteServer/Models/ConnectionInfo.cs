using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VRemoteServer.Models
{
    public class ConnectionInfo
    {
        public ConnectionInfo(ClientInfo sender, ClientInfo receiver)
        {
            Sender = sender;
            Receiver = receiver;
        }
        public ConnectionInfo(string sessionId, ClientInfo sender, ClientInfo receiver)
        {
            SessionId = sessionId;
            Sender = sender;
            Receiver = receiver;
        }
        public string SessionId { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 16);
        public ClientInfo Sender { get; set; }
        public ClientInfo Receiver { get; set; }
    }
}
