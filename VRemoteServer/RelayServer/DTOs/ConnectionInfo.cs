using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.Helpers;
using VRemoteServer.RelayServer.Networking;

namespace VRemoteServer.RelayServer.DTOs
{
    internal class ConnectionInfo
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

        [NotMapped]
        public SocketConnection SocketConnection { get; set; }
        public string ToNetworkString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var prop in this.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (Attribute.IsDefined(prop, typeof(NotMappedAttribute)))
                    continue;

                var value = prop.GetValue(this);
                sb.Append(value ?? string.Empty).Append(DefaultValue.Common.SEPRATOR);
            }
            return sb.ToString().TrimEnd(DefaultValue.Common.SEPRATOR);
        }
    }
}
