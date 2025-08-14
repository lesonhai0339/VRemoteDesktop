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
    public class ConnectionManager
    {
        private readonly object _lock = new object();
        private ConcurrentDictionary<string, ClientInfo> _connections;
        private ClientInfo _me;
        public ConnectionManager()
        {
            Me = InitMyInfo();
            _connections = new ConcurrentDictionary<string, ClientInfo>();
        }
        #region Properties
        public int NumberOfConnections => _connections.Count;
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
        public void UpdateMyInfo(byte[] data)
        {
            string myPublicIP= Helpers.ByteArrayHelper.ConvertByteArrayToString(data, Enums.EncodingType.ASCII).GetResult();
            Me.PublicIP = myPublicIP;
        }
        public bool AddConnection(string connectionId, ClientInfo client)
        {
            try
            {
                return _connections.TryAdd(connectionId, client);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", nameof(AddConnection))
                    .Error(ex, "Failed to add connection " + connectionId);
                return false;
            }
        }
        public bool RemoveConnection(string connectionId)
        {
            try
            {
                return _connections.TryRemove(connectionId, out _);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", nameof(RemoveConnection))
                    .Error(ex, "Failed to remove connection " + connectionId);
                return false;
            }
        }
        public List<ClientInfo> GetCurrentConnections()
        {
            return _connections.Values.ToList();
        }
        public bool ReceiveConnectionRequest(byte[] data)
        {
            //data should be have format: "type|myId|myPassword|partnerId|partnerLocalIP|partnerPublicIp|partnerListenerPort|partnerComputerName|partnerDisplayWidth|partnerDisplayHeight|partnerMajorVersion|partnerMinorVersion"
            string[] connectionData = Encoding.ASCII.GetString(data).Split('|');

            return true;
        }
        #endregion
    }
}
