using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VRemoteServer.Models;
using VRemoteServer.RelayServer.DTOs;
using VRemoteServer.RelayServer.Enums;
using VRemoteServer.RelayServer.Events;
using VRemoteServer.RelayServer.Helpers;
using VRemoteServer.RelayServer.Networking;
using static VRemoteServer.RelayServer.Helpers.DefaultValue.SocketConnectionDefault;

namespace VRemoteServer.RelayServer.Services
{
    public interface IRelayServerManager
    {
        void InitLoginServer();
        Task StartLoginServer(IPEndPoint ep);
        void CancelLoginServer();
        void InitRemoteControlServer();
        Task StartRemoteControlServer(IPEndPoint ep);
        void CancelRemoteControlServer();
        void Dispose();
    }
    public class RelayServerManagerService : IRelayServerManager, IDisposable
    {
        private bool _disposed; 
        private readonly ILoginManagerService _loginManager;
        private readonly IRemoteControlManagerService _remoteControlManager;
        public RelayServerManagerService(ILoginManagerService loginManagerService, IRemoteControlManagerService remoteControlManagerService)
        {
            _disposed = false;
            _loginManager = loginManagerService;
            _remoteControlManager = remoteControlManagerService;

            //Register events
            _loginManager.LoginManagerEvent += LoginManagerEventHandler;
            _remoteControlManager.RemoteControlManagerEvent += RemoteControlManagerEventHandler;
        }
        public void InitLoginServer()
        {
            _loginManager.InitServer();
        }
        public async Task StartLoginServer(IPEndPoint ep)
        {
            try
            {
                await _loginManager.StartServer(ep);
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "StartLoginServer error");
            }
        }
        public void CancelLoginServer()
        {
            _remoteControlManager.CancelServer();
        }
        public void InitRemoteControlServer()
        {
            _remoteControlManager.InitServer();
        }
        public async Task StartRemoteControlServer(IPEndPoint ep)
        {
            try
            {
                await _remoteControlManager.StartServer(ep);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "StartLoginServer error");
            }
        }
        public void CancelRemoteControlServer()
        {
            _remoteControlManager.CancelServer();
        }
        #region Events
        private void RemoteControlManagerEventHandler(object sender, RemoteControlManagerEventArgs e)
        {
            bool status = e.Type switch
            {
                SocketDataType.P2PRequestConnect => ProcessP2PRequestConnect(sender, e.SocketId, e.PartnerId, e.Data),
                SocketDataType.P2PAcceptConnect => ProcessP2PAcceptedConnect(sender, e.SocketId, e.DataOffset, e.DataLength),
                SocketDataType.P2PDisconnect => ProcessP2PDisconnected(sender),
                _ => ProcessP2PDataTransfer(sender, e.SocketId, e.Data, e.DataOffset, e.DataLength)
            };
            if (status)
            {

            }
            else
            {

            }
        }

        private bool ProcessP2PDisconnected(object sender)
        {
            if(sender is SocketConnection connection)
            {
                var remoteConnections = _remoteControlManager.GetRemoteConnectionsBySocketConnection(connection).ToArray();
                if(remoteConnections.Length != 0)
                {
                    foreach (var remoteConnection in remoteConnections)
                    {
                        try
                        {
                            var partner = ReferenceEquals(remoteConnection.Controller, connection) ? remoteConnection.Controlled : remoteConnection.Controller;
                            byte[] packet = PacketFactory.CreatePacket(SocketDataType.P2PDisconnect, remoteConnection.ConnectionId);
                            _remoteControlManager.Send(partner, packet);
                        }
                        finally
                        {
                            _remoteControlManager.RemoveRemoteConnection(remoteConnection.ConnectionId);
                        }
                    }
                }
            }
            return true;
        }

        private void LoginManagerEventHandler(object sender, LoginEventArgs e)
        {
            if(sender is SocketConnection connection)
            {
                switch (e.Type)
                {
                    case ServerEventType.ConnectionDisconnected:
                        ProcessSocketConnectionDisconnected(connection);
                        break;
                    default:
                        break;
                }
            }
        }
        private void ProcessSocketConnectionDisconnected(SocketConnection connection)
        {
            try
            {
                _loginManager.RemoveLogin(connection);
            }
            catch{ }
        }
        private bool ProcessP2PDataTransfer(object sender, string remoteConnectionId, byte[] data, int offset, int length)
        {
            if (sender is SocketConnection socketSender)
            {
                try
                {
                    if(_remoteControlManager.GetPartner(remoteConnectionId, socketSender, out SocketConnection socketReceive))
                    {
                        if(data != null)
                        {
                            _remoteControlManager.Send(socketReceive, data);
                        }
                        else
                        {
                            if (offset < 0 || length < 0)
                                return false;
                            _remoteControlManager.Send(socketReceive, offset, length);
                        }
                        return true;
                    }
                    else
                    {
                        byte[] packet = PacketFactory.CreatePacket(SocketDataType.P2PDataSendError, remoteConnectionId);
                        _remoteControlManager.Send(socketSender, packet);
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    Log.ForContext("FileName", this.GetType().Name).Error(ex, $"ProcessRemoteRequestToConnect error");
                }
            }
            return false;
        }

        private bool ProcessP2PAcceptedConnect(object sender, string remoteConnectionId, int offset, int length)
        {
            if (sender is SocketConnection controlled)
            {
                try
                {
                    if (_remoteControlManager.EstablishedRemoteConnection(remoteConnectionId, controlled, out var remoteConnection))
                    {
                        _remoteControlManager.Send(remoteConnection.Controller, offset, length);
                        return true;
                    }
                    else
                    {
                        _remoteControlManager.RemoveRemoteConnection(remoteConnectionId);
                    }
                    byte[] packet = PacketFactory.CreatePacket(SocketDataType.P2PConnectFailed, remoteConnectionId);
                    _remoteControlManager.Send(controlled, packet);
                    return false;
                }
                catch (Exception ex)
                {
                    Log.ForContext("FileName", this.GetType().Name).Error(ex, $"ProcessRemoteRequestToConnect error");
                }
            }
            return false;
        }

        private bool ProcessP2PRequestConnect(object sender, string connectionId, string partnerId, byte[] data)
        {
            if(sender is SocketConnection controller)
            {
                try
                {
                    if (_loginManager.TryGetLoggedConnection(partnerId, out var validConnection))
                    {
                        if (_remoteControlManager.InitRemoteConnection(connectionId, controller))
                        {
                            _remoteControlManager.Send(validConnection.SocketConnection, data);
                            return true;
                        }
                    }
                    _remoteControlManager.P2PConnectFailed(controller, connectionId);
                    return false;
                }
                catch (Exception ex)
                {
                    Log.ForContext("FileName", this.GetType().Name).Error(ex, $"ProcessRemoteRequestToConnect error");
                }
            }
            return false;
        }
        #endregion
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing || _disposed) return;

            try
            {

                try
                {
                    _loginManager?.Dispose();
                    _remoteControlManager?.Dispose();
                }
                catch { }
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}
