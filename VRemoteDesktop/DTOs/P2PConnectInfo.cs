using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Utils;

namespace VRemoteDesktop.Models
{
    [DataContract]
    public class P2PConnectInfo: BaseClass
    {
        public P2PConnectInfo() { }
        public P2PConnectInfo(string connectionId, string connectionPassword)
        {
            ConnectionId = connectionId;
            ConnectionPassword = connectionPassword;
        }
        [DataMember(Order = 0)] public string Id { get; set; } = StringHelper.RandomStringNumber(RandomLength.SOCKET_ID_LENGTH);
        [DataMember(Order = 1)] public string ConnectionId { get; set; }
        [DataMember(Order = 2)] public string ConnectionPassword { get; set; }    
    }
}
