using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.Helpers;
using VRemoteServer.RelayServer.Networking;

namespace VRemoteServer.RelayServer.DTOs
{
    [DataContract]
    public class ConnectionInfo: BaseClass
    {
        public ConnectionInfo() { }
        public ConnectionInfo(string id, 
            string password, 
            string defaultPassword,
            string computerName, 
            int width, 
            int height, 
            string majorVersion, 
            string minorVersion, 
            string ip, 
            string publicIP, 
            string port,
            SocketConnection socketConnection)
        {
            Id = id;
            Password = password;
            DefaultPassword = defaultPassword;
            ComputerName = computerName;
            Width = width;
            Height = height;
            MajorVersion = majorVersion;
            MinorVersion = minorVersion;
            Ip = ip;
            PublicIP = publicIP;
            Port = port;
            SocketConnection = socketConnection;
        }
        public void SetPublicIP(string publicIp)
        {
            if (string.IsNullOrEmpty(publicIp))
                throw new ArgumentNullException(nameof(publicIp));

            PublicIP = publicIp;
        }
        public void SetSocketConnection(SocketConnection sckConnection)
        {
            if (sckConnection == null)
                throw new ArgumentNullException(nameof(sckConnection));

            SocketConnection = sckConnection;
        }

        [DataMember(Order = 0)] public string Id { get; private set; }
        [DataMember(Order = 1)] public string Password { get; private set; }
        [DataMember(Order = 2)] public string DefaultPassword { get; private set; }
        [DataMember(Order = 3)] public string ComputerName { get; private set; }
        [DataMember(Order = 4)] public int Width { get; private set; }
        [DataMember(Order = 5)] public int Height { get; private set; }
        [DataMember(Order = 6)] public string MajorVersion { get; private set; }
        [DataMember(Order = 7)] public string MinorVersion { get; private set; }
        [DataMember(Order = 8)] public string Ip { get; private set; }
        [DataMember(Order = 9)] public string Port { get; private set; }
        [DataMember(Order = 10)] public string PublicIP { get; private set; }
        [NotMapped] public SocketConnection SocketConnection { get; private set; }
    }
}
