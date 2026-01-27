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
using VRemoteServer.Models;
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
        private ILoginManagerService login;
        private IRemoteControlManagerService remote;
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
        #region System
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
            catch (Exception ex)
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
                                    Log.Error(ex, "GetPartnerInfo err:");
                                }
                            });
                            break;
  
                        case SocketDataType.P2PReady:
                            P2PReadyHandler(sender, e.Data);
                            break;
                            //Can drop
                        case SocketDataType.P2PConnect:
                            P2PLogin(sender, e.Data);
                            break;
                        default:
                            break;
                    }
                }
            }
            catch(Exception ex)
            {
                Log.Error(ex, "Login server err ");
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
        private bool P2PLogin(object sender, byte[] data)
        {
            var connection = sender as SocketConnection;
            if (connection == null)
                return false;

            try
            {
                string[] dataParsed = Encoding.ASCII.ByteArrayToStringWithSeparator(data, DefaultValue.Common.SEPARATOR); //three
                P2PConnectInfo connectInfo = new P2PConnectInfo();
                if (connectInfo.TryParseData(dataParsed))
                {
                    if (_loginManager.GetFirst(connectInfo.ConnectionId, connectInfo.ConnectionPassword, out var validInfo))
                    {
                        //Send request to partner
                        var byteArray1 = PacketFactory.CreatePacket(SocketDataType.P2PConnect, data: Encoding.ASCII.StringToByteArray(connectInfo.Id));
                        bool respond = _loginManager.SendWithRespond(validInfo.SocketConnection, byteArray1);

                        if (respond)
                        {
                            //Send back to me
                            P2PNetworkInfo networkInfo = new P2PNetworkInfo
                            (
                                id: connectInfo.Id,
                                publicIP: validInfo.PublicIP,
                                localIP: validInfo.Ip,
                                port: validInfo.Port
                            );

                            byte[] dataSend = Encoding.ASCII.StringToByteArray(networkInfo.ToNetworkString());
                            var byteArray = PacketFactory.CreatePacket(SocketDataType.P2PDataRespond, data: dataSend);
                            return _loginManager.SendWithRespond(connection, byteArray);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, $"ProcessRemoteRequestToConnect error");
            }
            _loginManager.P2PConnectFailed(connection);
            return false;
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
                        RemoteDesktopControlDisconnected(sender);
                        break;

                    //Can drop
                    case SocketDataType.RemoteControlRequestToConnect:
                        RequestToRemoteDesktopControl(e.ConnectionId, sender, e.Data);
                        break;
                    case SocketDataType.RemoteControlAcceptedRequestToConnect:
                        AcceptedRequestToRemoteDesktopControl(e.ConnectionId, sender, e.Data);
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

        private bool RequestToRemoteDesktopControl(string connectionId, object sender, object data)
        {
            if (sender is SocketConnection controller)
            {
                if(data is SocketPacket packet)
                {
                    try
                    {
                        if (_loginManager.TryGetLoggedConnection(connectionId, out var validConnection))
                        {
                            if (_remoteControlManager.InitRemoteConnection(connectionId, controller))
                            {
                                _remoteControlManager.Send(validConnection.SocketConnection, packet.Offset, packet.Length);
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
            }
            return false;
        }

        private bool AcceptedRequestToRemoteDesktopControl(string connectionId, object sender, object data)
        {
            if (sender is SocketConnection controlled)
            {
                if(data is SocketPacket packet)
                {
                    try
                    {
                        if (_remoteControlManager.EstablishedRemoteConnection(connectionId, controlled, out var remoteConnection))
                        {
                            _remoteControlManager.Send(remoteConnection.Controller, packet.Offset , packet.Length);
                            return true;
                        }
                        else
                        {
                            _remoteControlManager.RemoveRemoteConnection(connectionId);
                        }
                        byte[] failed = PacketFactory.CreatePacket(SocketDataType.RemoteControlConnectFailed, connectionId);
                        _remoteControlManager.Send(controlled, failed);
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

        private bool RemoteDesktopControlDisconnected(object sender)
        {
            if (sender is SocketConnection connection)
            {
                var remoteConnections = _remoteControlManager.GetRemoteConnectionsBySocketConnection(connection).ToArray();
                if (remoteConnections.Length != 0)
                {
                    foreach (var remoteConnection in remoteConnections)
                    {
                        try
                        {
                            var partner = ReferenceEquals(remoteConnection.Controller, connection) ? remoteConnection.Controlled : remoteConnection.Controller;
                            byte[] packet = PacketFactory.CreatePacket(SocketDataType.RemoteControlDisconnect, remoteConnection.ConnectionId);
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

        private bool RemoteDesktopControlDataForward(string connectionId, object sender, object data)
        {
            if (sender is SocketConnection socketSender)
            {
                if(data is SocketPacket packet)
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
            if (!disposing || _disposed) return;
            try
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
                catch { }
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}
