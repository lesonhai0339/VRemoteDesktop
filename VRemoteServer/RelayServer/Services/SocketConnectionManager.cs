using Serilog;
using System;
using System.Net;
using System.Text;
using VRemoteServer.RelayServer.Domains;
using VRemoteServer.RelayServer.Helpers;
using VRemoteServer.RelayServer.Networking;
using ConnectionInfo = VRemoteServer.RelayServer.DTOs.ConnectionInfo;
using static VRemoteServer.RelayServer.Helpers.DefaultValue.SocketConnectionDefault;
using VRemoteServer.RelayServer.Enums;


namespace VRemoteServer.RelayServer.Services
{
    /// <summary>
    /// Manager socket client connect to server
    /// </summary>
    public interface ISocketConnectionManager : IBaseManagement<ConnectionInfo>
    {
        bool NewConnectionInfo(byte[] data, SocketConnection socketConnection, out ConnectionInfo connectionInfo);
    }
    public class SocketConnectionManager: BaseManagement<ConnectionInfo>, ISocketConnectionManager, IDisposable 
    {
        public bool NewConnectionInfo(byte[] data, SocketConnection socketConnection, out ConnectionInfo connectionInfo)
        {
            connectionInfo = null;

            if (data == null || data.Length == 0)
                return false;

            if (socketConnection == null)
                return false;

            string[] rawInfo = Encoding.ASCII.ByteArrayToStringWithSeparator(data, DefaultValue.Common.SEPARATOR);        
            connectionInfo = new ConnectionInfo();
            bool parseRespond = connectionInfo.TryParseData(rawInfo);
            if (!parseRespond)
            {
                return false;
            }
            connectionInfo.PublicIP = socketConnection.IP;
            connectionInfo.SocketConnection = socketConnection;
            return Add(connectionInfo.Id, connectionInfo);
        }
    }
}
