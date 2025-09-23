using Serilog;
using System;
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
    public interface ISocketConnectionManager : IBaseManagement<ConnectionInfo>
    {
        bool NewConnectionInfo(byte[] data, SocketConnection socketConnection, out ConnectionInfo connectionInfo);
    }
    public class SocketConnectionManager: BaseManagement<ConnectionInfo>, ISocketConnectionManager, IDisposable 
    {
        public event EventHandler<SocketConnectionManagerEventArg> SocketConnectionManagerEvent;
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
            connectionInfo.SocketConnection.IsReceivedFirstPacket = true;
            socketConnection.SocketConnectionEvent += SocketConnectionEventHandler;
            return Add(connectionInfo.Id, connectionInfo);
        }
        /// <summary>
        /// remove connectionInfo and unregister event
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public override bool Remove(string id)
        {
            if(base.TakeAndRemote(id, out var connectionInfo))
            {
                connectionInfo.SocketConnection.SocketConnectionEvent -= SocketConnectionEventHandler;
                return true;
            }
            return false;
        }
        private void SocketConnectionEventHandler(object sender, SocketConnectionEventArg e)
        {
            SocketConnectionManagerEvent?.Invoke(sender, new SocketConnectionManagerEventArg(SocketConnectionManagerEventType.DataReceived, e));
        }
        public override void Dispose()
        {
            foreach(var connectionInfo in GetAll())
            {
                lock (connectionInfo)
                {
                    connectionInfo.SocketConnection.SocketConnectionEvent -= SocketConnectionEventHandler;    
                }
            }
        }
    }
}
