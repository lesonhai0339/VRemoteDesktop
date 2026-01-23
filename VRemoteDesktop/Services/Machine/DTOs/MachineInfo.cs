using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using VRemoteDesktop.Models;

namespace VRemoteDesktop.Services.Machine.DTOs
{
    [DataContract]
    public class MachineInfo : BaseClass
    {
        public MachineInfo() { }
        public MachineInfo(
            string id,
            string password,
            string defaultPassword,
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
            DefaultPassword = defaultPassword;
            ComputerName = computerName;
            Width = width;
            Height = height;
            MajorVersion = majorVersion;
            MinorVersion = minorVersion;
            Ip = ip;
            PublicIP = publicIP;
            Port = port;
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
        public void UpdateLocalIp(string localIp)
        {
            if (!string.IsNullOrEmpty(localIp))
                throw new ArgumentNullException("LocalIp cannot be null or empty");

            Ip = localIp;
        }

        public void UpdatePublicIp(string publicIp)
        {
            if (!string.IsNullOrEmpty(publicIp))
                throw new ArgumentNullException("PublicIp cannot be null or empty");

            PublicIP = publicIp;    
        }
        public void UpdatePort(string port)
        {
            if (!string.IsNullOrEmpty(port))
                throw new ArgumentNullException("Port cannot be null or empty");

            Port = port;
        }
    }
}
