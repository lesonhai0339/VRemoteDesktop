using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.DTOs.Response
{
    public class PartnerNetworkInfo
    {
        public PartnerNetworkInfo(string sessionId, string partnerId, string partnerPassword, string publicIP, string localIP, string port, int width, int height)
        {
            SessionId = sessionId;
            PartnerId = partnerId;
            PartnerPassword = partnerPassword;
            PublicIP = publicIP;
            LocalIP = localIP;
            Port = port;
            Width = width;
            Height = height;
        }
        public string SessionId { get; set; }
        public string PartnerId { get; set; }
        public string PartnerPassword { get; set; }
        public string PublicIP { get; set; }
        public string LocalIP { get; set; }
        public string Port { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}
