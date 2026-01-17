using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using VRemoteServer.RelayServer.Domains;
using VRemoteServer.RelayServer.Enums;
using VRemoteServer.RelayServer.Events;
using VRemoteServer.RelayServer.Helpers;
using VRemoteServer.RelayServer.Networking;
using static VRemoteServer.RelayServer.Helpers.DefaultValue.SocketConnectionDefault;
using ConnectionInfo = VRemoteServer.RelayServer.DTOs.ConnectionInfo;


namespace VRemoteServer.RelayServer.Services
{
    /// <summary>
    /// Manager socket client connect to server
    /// </summary>
    public interface ILoginManager : IBaseManagement<ConnectionInfo>
    {
        bool RemoveLoginInfoBySocketConnection(SocketConnection connection);
        bool GetConnectionsInfoBySocketConnection(SocketConnection connection, out List<ConnectionInfo> connectionsInfo);
        bool NewConnectionInfo(byte[] data, SocketConnection socketConnection, out ConnectionInfo connectionInfo);
    }
    public class LoginManager: BaseManagement<ConnectionInfo>, ILoginManager, IDisposable 
    {
        public bool RemoveLoginInfoBySocketConnection(SocketConnection connection)
        {
            try
            {
                var loginsInfo = GetAll(v => ReferenceEquals(v.SocketConnection, connection)).ToList();
                bool removed = false;
                foreach (var loginInfo in loginsInfo)
                {
                    if (base.Remove(loginInfo))
                        removed = true;
                }
                return removed;
            }
            catch 
            {
                return false;
            }
        }

        public bool GetConnectionsInfoBySocketConnection(SocketConnection connection, out List<ConnectionInfo> connectionsInfo)
        {
            connectionsInfo = base.GetAll().Where(x => ReferenceEquals(x.SocketConnection, connection)).ToList();
            if(connectionsInfo.Count > 0)
            {
                return true;
            }
            return false;
        }

        public bool NewConnectionInfo(byte[] data, SocketConnection socketConnection, out ConnectionInfo connectionInfo)
        {
            connectionInfo = null;
            try
            {
                if (data == null || data.Length == 0)
                    return false;

                if (socketConnection == null)
                    return false;

                connectionInfo = new ConnectionInfo();
                bool parseRespond = connectionInfo.TryParseDataWithSeparator(data, Encoding.ASCII, DefaultValue.Common.SEPARATOR);
                if (!parseRespond)
                {
                    return false;
                }
                connectionInfo.SetPublicIP(socketConnection.IP);
                connectionInfo.SetSocketConnection(socketConnection);
                return Add(connectionInfo.Id, connectionInfo);
            }
            catch (ArgumentNullException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
