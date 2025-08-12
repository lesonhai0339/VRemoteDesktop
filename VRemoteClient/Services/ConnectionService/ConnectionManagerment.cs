using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteClient.Models.Entities;
using VRemoteClient.Utils;

namespace VRemoteClient.Services.ConnectionService
{
    public static class ConnectionManagerment
    {
        private static ConcurrentDictionary<string, ClientInfo> _chatConnections = new ConcurrentDictionary<string, ClientInfo>();
        private static ClientInfo _me = InitInfo();
        #region Properties
        public static int NumberOfConnections => _chatConnections.Count;
        public static ClientInfo Me => _me;
        #endregion
        #region Methods
        private static ClientInfo InitInfo()
        {
            var computerName = Environment.MachineName;
            int width = Screen.PrimaryScreen.Bounds.Width;
            int height = Screen.PrimaryScreen.Bounds.Height;
            OperatingSystem os = Environment.OSVersion;
            ClientInfo info = new ClientInfo
            {
                Id = StringBuilderUtils.RandomStringNumber(8),
                Password = StringBuilderUtils.RandomStringNumber(4),
                ComputerName = computerName,
                Width = width,
                Height = height,
                MajorVersion = os.Version.Major.ToString(),
                MinorVersion = os.Version.Minor.ToString()
            };
            return info;
        }
        public static bool AddConnection(string connectionId, ClientInfo client)
        {
            try
            {
                return _chatConnections.TryAdd(connectionId, client);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", nameof(ConnectionManagerment))
                    .Error(ex, "Failed to add connection " + connectionId);
                return false;
            }
        }
        public static bool RemoveConnection(string connectionId)
        {
            try
            {
                return _chatConnections.TryRemove(connectionId, out _);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", nameof(ConnectionManagerment))
                    .Error(ex, "Failed to remove connection " + connectionId);
                return false;
            }
        }
        public static List<ClientInfo> GetCurrentConnections()
        {
            return _chatConnections.Values.ToList();
        }
        public static bool ReceiveConnectionRequest(byte[] data)
        {
            //data should be have format: "type|myId|myPassword|partnerId|partnerLocalIP|partnerPublicIp|partnerListenerPort|partnerComputerName|partnerDisplayWidth|partnerDisplayHeight|partnerMajorVersion|partnerMinorVersion"
            string[] connectionData = Encoding.ASCII.GetString(data).Split('|');

            return true;
        }
        #endregion
    }
}
