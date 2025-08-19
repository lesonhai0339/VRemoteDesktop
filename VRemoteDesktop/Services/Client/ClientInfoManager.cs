using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Models;
using static VRemoteDesktop.Utils.Logger;
using VRemoteServer.Models;

namespace VRemoteDesktop.Services.ConnectionManager
{
    public interface IClientInfoManager
    {
        ClientInfo GetMyInfo();
        string GetLocalIPAddress();
        void UpdateMyInfo(byte[] publicIp);
        void UpdateMyInfo(ClientInfo info);
    }
    public class ClientInfoManager: IClientInfoManager
    {
        private readonly object _lock = new object();
        private ClientInfo _me;
        public ClientInfoManager()
        {
            Me = InitMyInfo();
        }
        #region Properties
        public ClientInfo Me
        {
            get
            {
                lock (_lock)
                {
                    return _me;
                }
            }
            private set
            {
                lock (_lock)
                {
                    _me = value;
                }
            }
        }
        #endregion
        #region Methods
        public ClientInfo GetMyInfo()
        {
            return Me;  
        }
        private ClientInfo InitMyInfo()
        {
            var computerName = Environment.MachineName;
            int width = Screen.PrimaryScreen.Bounds.Width;
            int height = Screen.PrimaryScreen.Bounds.Height;
            OperatingSystem os = Environment.OSVersion;
            ClientInfo info = new ClientInfo
            {
                Id = StringHelper.RandomStringNumber(8),
                Password = StringHelper.RandomStringNumber(4),
                ComputerName = computerName,
                Width = width,
                Height = height,
                MajorVersion = os.Version.Major.ToString(),
                MinorVersion = os.Version.Minor.ToString(),
                Ip = GetLocalIPAddress(),
                Port = AppSettingHelper.Getvalue("RemoteServerPort"),
                PublicIP = ""
            };
            return info;
        }
        public string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            return "";
        }
        public void UpdateMyInfo(byte[] publicIp)
        {
            string myPublicIP= Helpers.ByteArrayHelper.ConvertByteArrayToString(publicIp, Enums.EncodingType.ASCII).GetResult();
            lock (_lock)
            {
                _me.PublicIP = myPublicIP;
            }
        }
        public void UpdateMyInfo(ClientInfo info)
        {
            lock (_lock)
            {
                _me.Id = info.Id ?? Me.Id;
                _me.Password = info.Password ?? _me.Password;
                _me.ComputerName = info.ComputerName ?? _me.ComputerName;
                _me.Width = (info.Width != 0) ? info.Width : _me.Width;
                _me.Height = (info.Height != 0) ? info.Height : _me.Height;
                _me.MajorVersion = info.MajorVersion ?? _me.MajorVersion;
                _me.MinorVersion = info.MinorVersion ?? _me.MinorVersion;
                _me.Ip = info.Ip ?? _me.Ip;
                _me.PublicIP = info.PublicIP ?? _me.PublicIP;
                _me.Port = info.Port ?? _me.Port;
            }
        }
        #endregion
    }
}
