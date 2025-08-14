using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteClient.Models.Entities
{
    public class ConnectionInfo
    {
        public ConnectionInfo() { }
        public ConnectionInfo(string sessionId)
        {
            SessionId = sessionId;
        }
        public ConnectionInfo(ClientInfo partner, ClientInfo me, bool isSender)
        {
            Partner = partner;
            Me = me;
            IsSender = isSender;
        }
        public ConnectionInfo(string sessionId, ClientInfo partner, ClientInfo me, bool isSender)
        {
            SessionId = sessionId;
            Partner = partner;
            Me = me;
            IsSender = isSender;
        }
        public string SessionId { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 16);
        public ClientInfo Me { get; set; }
        public ClientInfo Partner { get; set; }
        public bool IsSender { get; set; } = false;
    }
}
