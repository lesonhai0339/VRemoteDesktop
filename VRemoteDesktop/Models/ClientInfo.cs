using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace VRemoteServer.Models
{
    public class ClientInfo
    {
        public ClientInfo() { }
        public ClientInfo(string id, string password, string computerName, int width, int height, string majorVersion, string minorVersion, string ip, string publicIP, string port)
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

        public string Id { get; set; }
        public string Password { get; set; }
        public string ComputerName { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string MajorVersion { get; set; }
        public string MinorVersion { get; set; }
        public string Ip { get; set; }
        public string Port { get; set; }
        public string PublicIP { get; set; }
        public string ToNetworkString()
        {
            return string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}", Id, Password, ComputerName, Width, Height, MajorVersion, MinorVersion, Ip, Port, PublicIP);
        }
    }
}
