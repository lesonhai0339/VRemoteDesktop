using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using VRemoteDesktop.Models;
using VRemoteDesktop.Utils;

namespace VRemoteServer.Models
{
    [DataContract]
    public class ClientInfo: BaseClass
    {
        public ClientInfo() { }
        public ClientInfo(string id,
            string password,
            string computerName,
            int width,
            int height,
            string majorVersion,
            string minorVersion,
            string ip,
            string publicIP,
            string port)
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
    }
}
