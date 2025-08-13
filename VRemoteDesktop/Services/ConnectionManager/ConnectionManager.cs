using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Models;
using static VRemoteDesktop.Utils.Logger;

namespace VRemoteDesktop.Services.ConnectionManager
{
    internal static class ConnectionManager
    {
        private static ConcurrentDictionary<string, Client> _chatConnections = new ConcurrentDictionary<string, Client>();
        private static Client _me = InitOwnerInfomation();
        #region Properties
        public static int NumberOfConnections => _chatConnections.Count;
        public static Client Me => _me;
        #endregion
        #region Methods
        private static Client InitOwnerInfomation()
        {
            var computerName = Environment.MachineName;
            int width = Screen.PrimaryScreen.Bounds.Width;
            int height = Screen.PrimaryScreen.Bounds.Height;
            OperatingSystem os = Environment.OSVersion;
            Client info = new Client
            {
                Id = StringHelper.RandomStringNumber(8),
                Password = StringHelper.RandomStringNumber(4),
                ComputerName = computerName,
                Width = width,
                Height = height,
                MajorVersion = os.Version.Major.ToString(),
                MinorVersion = os.Version.Minor.ToString()
            };
            return info;
        }
        public static bool AddConnection(string connectionId, Client client)
        {
            try
            {
                return _chatConnections.TryAdd(connectionId, client);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", nameof(AddConnection))
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
                Log.ForContext("FileName", nameof(RemoveConnection))
                    .Error(ex, "Failed to remove connection " + connectionId);
                return false;
            }
        }
        public static List<Client> GetCurrentConnections()
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
