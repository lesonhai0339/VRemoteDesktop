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
        [DataMember(Order = 0)] public string Id { get; set; }
        [DataMember(Order = 1)] public string Password { get; set; }
        [DataMember(Order = 2)] public string ComputerName { get; set; }
        [DataMember(Order = 3)] public int Width { get; set; }
        [DataMember(Order = 4)] public int Height { get; set; }
        [DataMember(Order = 5)] public string MajorVersion { get; set; }
        [DataMember(Order = 6)] public string MinorVersion { get; set; }
        [DataMember(Order = 7)] public string Ip { get; set; }
        [DataMember(Order = 8)] public string Port { get; set; }
        [DataMember(Order = 9)] public string PublicIP { get; set; }
        [NotMapped] public SocketConnection SocketConnection { get; set; }
    }
}
