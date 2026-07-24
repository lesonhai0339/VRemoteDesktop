using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vsign4.VRemoteDesktop.DTOs.Responses
{
    public class PartnerNetworkInfo
    {
        public PartnerNetworkInfo(string sessionId, string partnerId, string partnerPassword, string publicIP, string localIP, string port, int width, int height, string computerName, string major, string minor)
        {
            SessionId = sessionId;
            PartnerId = partnerId;
            PartnerPassword = partnerPassword;
            PublicIP = publicIP;
            LocalIP = localIP;
            Port = port;
            Width = width;
            Height = height;
            ComputerName = computerName;
            MajorVersion = major;
            MinorVersion = minor;
        }
        public string SessionId { get; set; }
        public string PartnerId { get; set; }
        public string PartnerPassword { get; set; }
        public string PublicIP { get; set; }
        public string LocalIP { get; set; }
        public string Port { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string ComputerName { get; set; }
        public string MajorVersion { get; set; }
        public string MinorVersion { get; set; }
    }
}
