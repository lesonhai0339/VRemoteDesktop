using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteClient.Models.Entities;
using VRemoteClient.Models.Enums;
using VRemoteClient.Utils;

namespace VRemoteClient.Services.ConnectionService
{
    /// <summary>
    /// Manager remote connections to this computer
    /// </summary>
    public class ConnectionManager
    {
        private ConcurrentDictionary<string, ConnectionInfo> _currentConnections;
        public int NumberOfConnections => _currentConnections.Count;

        public ConnectionManager()
        {
            _currentConnections = new ConcurrentDictionary<string, ConnectionInfo>();
        }
        public List<ConnectionInfo> GetCurrentConnections()
        {
            return _currentConnections.Values.ToList();
        }
        public ConnectionInfo ConvertFromBytes(byte[] data, int offset, int length)
        {
            try
            {
                byte[] rawData = new byte[length];
                Buffer.BlockCopy(data, offset, rawData, 0, length);

                string[] dataEncoded = Encoding.ASCII.GetString(rawData).Split('|');

                ConnectionInfo connectionInfo = new ConnectionInfo(
                    sessionId: dataEncoded[1]
                );

                ClientInfo info = new ClientInfo
                {
                    Id = dataEncoded[2],
                    Password = dataEncoded[3],
                    ComputerName = dataEncoded[4],
                    Width = int.Parse(dataEncoded[5]),
                    Height = int.Parse(dataEncoded[6]),
                    MajorVersion = dataEncoded[7],
                    MinorVersion = dataEncoded[8],
                };
                if (int.TryParse(dataEncoded[0], out int type) && (ClientType)type == ClientType.RECEIVER)
                {
                    connectionInfo.IsSender = true;
                }
                connectionInfo.Partner = info;

                return connectionInfo;
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", "ConnectionManager").Error(ex, "Error when parse data");
                return null;
            }
        }
        public bool AddConnection(string sessionId, ConnectionInfo info)
        {
            bool flag = _currentConnections.TryAdd(sessionId, info);
            return flag;
        }
        public bool RemoveConnection(string sessionId)
        {
            bool flag = _currentConnections.TryRemove(sessionId, out var _);
            return flag;
        }
        public void Clear()
        {
            _currentConnections.Clear();
        }
    }
}
