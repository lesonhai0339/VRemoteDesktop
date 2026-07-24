using Newtonsoft.Json;
using Serilog;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.DTOs;
using VRemoteServer.RelayServer.DTOs.Requests;
using VRemoteServer.RelayServer.DTOs.Responses;
using VRemoteServer.RelayServer.Enums;
using VRemoteServer.RelayServer.Events;
using VRemoteServer.RelayServer.Helpers;
using VRemoteServer.RelayServer.Networking;

namespace VRemoteServer.RelayServer.Services
{
    public interface IRelayServerManager
    {
        void LinkedToken(CancellationToken token);
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
        private int _disposed = 0;
        private ILoginManagerService login;
        private IRemoteControlManagerService remote;
        private readonly ILoginManagerService _loginManager;
        private readonly IRemoteControlManagerService _remoteControlManager;

        private CancellationTokenSource _cancellationTokenSource;
        public RelayServerManagerService(ILoginManagerService loginManagerService, IRemoteControlManagerService remoteControlManagerService)
        {
            _loginManager = loginManagerService;
            _remoteControlManager = remoteControlManagerService;

            //Register events
            _loginManager.LoginManagerEvent += LoginManagerEventHandler;
            _remoteControlManager.RemoteControlManagerEvent += RemoteControlManagerEventHandler;
        }
        public void LinkedToken(CancellationToken token)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
        }
        #region System
        public void InitLoginServer()
        {
            _loginManager.InitServer();
        }
        public async Task StartLoginServer(IPEndPoint ep)
        {
            try
            {
                await _loginManager.StartServer(ep, _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "StartLoginServer error");
            }
        }

        public void CancelLoginServer()
        {
            // Was cancelling the remote-control server (copy-paste bug) — the login server
            // then never shut down and kept its port bound.
            _loginManager.CancelServer();
        }

        public void InitRemoteControlServer()
        {
            _remoteControlManager.InitServer();
        }

        public async Task StartRemoteControlServer(IPEndPoint ep)
        {
            try
            {
                await _remoteControlManager.StartServer(ep, _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "StartRemoteControlServer error");
            }
        }

        public void CancelRemoteControlServer()
        {
            _remoteControlManager.CancelServer();
        }
        #endregion

        #region Events

        #region Login Server
        private void LoginManagerEventHandler(object sender, LoginEventArgs e)
        {
            try
            {
                if (sender is SocketConnection connection)
                {
                    switch (e.Type)
                    {
                        case SocketDataType.Ping:
                            Ping(connection);
                            break;
                        case SocketDataType.Login:
                            Login(connection, e.Data);
                            break;
                        case SocketDataType.Disconnect:
                            LoginUserDisconnected(connection);
                            break;
                        case SocketDataType.GetPartnerInfo:
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await GetPartnerInfo(connection, e.Data);
                                }
                                catch (Exception ex)
                                {
                                    Log.ForContext("FileName", this.GetType().Name).Error(ex, "GetPartnerInfo err:");
                                }
                            });
                            break;
  
                        case SocketDataType.P2PReady:
                            P2PReadyHandler(sender, e.Data);
                            break;
                        default:
                            break;
                    }
                }
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "Login server err ");
            }
        }

        private void P2PReadyHandler(object sender, byte[] data)
        {
            if(sender is SocketConnection connection)
            {
                _loginManager.TaskCompleted(connection, data);
            }
        }

        private async Task GetPartnerInfo(SocketConnection connection, byte[] data)
        {
            if (connection == null)
                return;
            try
            {
                var partnerCredentials = JsonConvert.DeserializeObject<PartnerCredentials>(Encoding.ASCII.GetString(data));

                if (partnerCredentials == null)
                {
                    _loginManager.GetPartnerInfoFailed(connection, "Invalid data");
                    return;
                }
                if (_loginManager.GetFirst(partnerCredentials.Id, partnerCredentials.Password, out var partner))
                {
                    if (_loginManager.GetConnectionsInfoBySocketConnection(connection, out var me))
                    {
                        if (_remoteControlManager.CreateRoomId(out string id))
                        {
                            await _loginManager.InitRemoteConnection(me.First(), partner, id);
                            return;
                        }
                    }
                }
                _loginManager.GetPartnerInfoFailed(connection, "Get partner info failed");
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, $"ProcessRemoteRequestToConnect error");
            }
        }
        private void Ping(SocketConnection connection)
        {
            try
            {
                _loginManager.Ping(connection);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "Ping");
            }
        }

        private void Login(SocketConnection connection, byte[] data)
        {
            try
            {
                if (_loginManager.Add(connection, data, out ConnectionInfo connectionInfo))
                {
                    _loginManager.LoginResponse(connection, connectionInfo, true);
                }
                else
                {
                    _loginManager.LoginResponse(connection, null, false);
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "Login");
            }
        }

        private void LoginUserDisconnected(SocketConnection connection)
        {
            try
            {
                _loginManager.RemoveLogin(connection);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "RemoteControlManagerEventHandler");
            }
        }
        #endregion server Server



        #region Turn Server
        private void RemoteControlManagerEventHandler(object sender, RemoteControlManagerEventArgs e)
        {
            try
            {
                switch (e.Type)
                {
                    case SocketDataType.RemoteLogin:
                        RemoteLoginHandler(e.ConnectionId, sender, e.Data);
                        break;
                    case SocketDataType.RemoteControlDisconnect:
                        RemoteDesktopControlDisconnected(e.ConnectionId, sender);
                        break;
                    default:
                        RemoteDesktopControlDataForward(e.ConnectionId, sender, e.Data);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "RemoteControlManagerEventHandler");
            }
        }

        private void RemoteLoginHandler(string connectionId, object sender, object data)
        {
            if(sender is SocketConnection connection)
            {
                if (data is SocketPacket packet)
                {
                    _remoteControlManager.AddOrUpdateRemoteControl(connectionId, connection, packet);
                }
            }   
        }
        private void RemoteDesktopControlDisconnected(string sessionId, object sender)
        {
            if (sender is SocketConnection connection)
            {
                _remoteControlManager.RemoveRemoteConnection(sessionId, connection);
            }
        }
        private bool RemoteDesktopControlDataForward(string connectionId, object sender, object data)
        {
            if (sender is SocketConnection socketSender)
            {
                if (data is SocketPacket packet)
                {
                    try
                    {
                        if (_remoteControlManager.GetPartner(connectionId, socketSender, out SocketConnection socketReceive))
                        {
                            _remoteControlManager.Send(socketReceive, packet.Offset, packet.Length);
                        }
                        else
                        {
                            byte[] failed = PacketFactory.CreatePacket(SocketDataType.RemoteControlDataSendFailed, connectionId);
                            _remoteControlManager.Send(socketSender, failed);
                        }
                        return false;
                    }
                    catch (Exception ex)
                    {
                        Log.ForContext("FileName", this.GetType().Name).Error(ex, $"ProcessRemoteRequestToConnect error");
                    }
                }
            }
            return false;
        }
        #endregion

        #endregion
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;

            if (disposing)
            {
                try
                {
                    if (_loginManager != null)
                        _loginManager.LoginManagerEvent -= LoginManagerEventHandler;

                    if (_remoteControlManager != null)
                        _remoteControlManager.RemoteControlManagerEvent -= RemoteControlManagerEventHandler;

                    _loginManager?.Dispose();
                    _remoteControlManager?.Dispose();
                }
                catch(Exception ex)
                {
                    Log.ForContext("FileName", this.GetType().Name).Error(ex, "RelayServer err ");
                }
            }
            
        }
    }
}
