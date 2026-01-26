using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.DTOs.Response
{
    public class PartnerNetworkInfo
    {
        public PartnerNetworkInfo(string sessionId, string publicIP, string localIP, string port)
        {
            SessionId = sessionId;
            PublicIP = publicIP;
            LocalIP = localIP;
            Port = port;
        }
        public string SessionId { get; set; }
        public string PublicIP { get; set; }
        public string LocalIP { get; set; }
        public string Port { get; set; }
    }
}
