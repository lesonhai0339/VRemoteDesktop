using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Models
{
    public class Client
    {
        public Client() { }
        public Client(string id, string password, string computerName, int width, int height, string majorVersion, string minorVersion, string ip, string port)
        {
            Id = id;
            Password = password;
            ComputerName = computerName;
            Width = width;
            Height = height;
            MajorVersion = majorVersion;
            MinorVersion = minorVersion;
            Ip = ip;
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
        public string ToNetworkPacketString()
        {
            return string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}", Id, Password, ComputerName, Width, Height, MajorVersion, MinorVersion);
        }
    }
}
