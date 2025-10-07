using Serilog;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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

        //Login
        private void LoginManagerEventHandler(object sender, LoginEventArgs e)
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
                    case SocketDataType.P2PRequestToConnect:
                        P2PLogin(sender, e.Data);
                        break;
                    default:
                        break;
                }
            }
        }
        private bool P2PLogin(object sender,byte[] data)
        {
            if (sender is SocketConnection me)
            {
                try
                {
                    string[] partnerIdAndPassword = Encoding.ASCII.ByteArrayToStringWithSeparator(data, '|'); //three
                    if (_loginManager.GetFirst(partnerIdAndPassword[1], partnerIdAndPassword[2], out var validInfo))
                    {
                        //Send request to partner
                        var byteArray1 = PacketFactory.CreatePacket(SocketDataType.P2PRequestToConnect, data: Encoding.ASCII.StringToByteArray(partnerIdAndPassword[0]));
                        bool respond = _loginManager.SendWithRespond(validInfo.SocketConnection, byteArray1);

                        if (respond)
                        {
                            //Send back to me
                            byte[] dataSend = Encoding.ASCII.StringArrayToByteArrayWithSeparator('|', partnerIdAndPassword[0], validInfo.PublicIP, validInfo.Ip, validInfo.Port);
                            var byteArray = PacketFactory.CreatePacket(SocketDataType.P2PRespondRequestToConnect, data: dataSend);
                            _loginManager.SendWithRespond(me, byteArray);
                        }
                    }
                    //_remoteControlManager.P2PConnectFailed(controller, connectionId);
                    //return false;
                }
                catch (Exception ex)
                {
                    Log.ForContext("FileName", this.GetType().Name).Error(ex, $"ProcessRemoteRequestToConnect error");
                }
            }
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
                if(_loginManager.Add(connection, data, out ConnectionInfo connectionInfo))
                {
                    _loginManager.LoginSucceeded(connection, connectionInfo);
                }
                else
                {
                    _loginManager.LoginFailed(connection);
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
        //Remote Control
        private void RemoteControlManagerEventHandler(object sender, RemoteControlManagerEventArgs e)
        {
            try
            {
                switch (e.Type)
                {
                    case SocketDataType.RemoteControlRequestToConnect:
                        RequestToRemoteDesktopControl(sender, e.SocketId, e.PartnerId, e.Data);
                        break;
                    case SocketDataType.RemoteControlAcceptedRequestToConnect:
                        AcceptedRequestToRemoteDesktopControl(sender, e.SocketId, e.DataOffset, e.DataLength);
                        break;
                    case SocketDataType.RemoteControlDisconnect:
                        RemoteDesktopControlDisconnected(sender);
                        break;
                    default:
                        RemoteDesktopControlDataForward(sender, e.SocketId, e.Data, e.DataOffset, e.DataLength);
                        break;
                }
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "RemoteControlManagerEventHandler");
            }
        }
        private bool RequestToRemoteDesktopControl(object sender, string connectionId, string partnerId, byte[] data)
        {
            if (sender is SocketConnection controller)
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
        private bool AcceptedRequestToRemoteDesktopControl(object sender, string remoteConnectionId, int offset, int length)
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
                    byte[] packet = PacketFactory.CreatePacket(SocketDataType.RemoteControlConnectFailed, remoteConnectionId);
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

        private bool RemoteDesktopControlDisconnected(object sender)
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
        private bool RemoteDesktopControlDataForward(object sender, string remoteConnectionId, byte[] data, int offset, int length)
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
                        byte[] packet = PacketFactory.CreatePacket(SocketDataType.RemoteControlDataSendFailed, remoteConnectionId);
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
