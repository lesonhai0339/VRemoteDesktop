using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.Helpers;

namespace VRemoteServer.RelayServer.DTOs
{
    [DataContract]
    public class P2PNetworkInfo: BaseClass
    {
        public P2PNetworkInfo() { }
        public P2PNetworkInfo(string id, string publicIP, string localIP, string port)
        {
            Id = id;
            PublicIP = publicIP;
            LocalIP = localIP;
            Port = port;
        }
        [DataMember(Order = 0)] public string Id { get; set; }
        [DataMember(Order = 1)] public string PublicIP { get; set; }
        [DataMember(Order = 2)] public string LocalIP { get; set; }
        [DataMember(Order = 3)] public string Port { get; set; }
    }
}
