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
    public class RelayServerManager : IRelayServerManager, IDisposable
    {
        private bool _disposed; 
        private readonly ILoginManager _loginManager;
        private readonly IRemoteControlManager _remoteControlManager;
        public RelayServerManager(ILoginManager loginManager, IRemoteControlManager remoteControlManager)
        {
            _disposed = false;
            _loginManager = loginManager;
            _remoteControlManager = remoteControlManager;

            //Register events
            _remoteControlManager.RemoteControlManagerEvent += RemoteControlManagerEventHandler;
            //_server.ServerEvent += ServerEventHandler;
            //_socketConnectionManager.SocketConnectionManagerEvent += SocketConnectionManagerEventHandler;
            //_remoteConnectionManager.remoteSocketManagerEvent += RemoteSocketManagerEventHandler;

            ////Register methods
            //_systemMethods = new Dictionary<SocketDataType, Action<SocketConnection, int, int>>
            //{
            //    {SocketDataType.Connect, ProcessConnect },
            //    {SocketDataType.Login, ProcessLogin},
            //    {SocketDataType.P2PRequestConnect, ProcessRemoteRequestToConnect},
            //    {SocketDataType.P2PAcceptConnect, ProcessRemoteAcceptedToConnect},
            //    {SocketDataType.P2PRejectConnect, ProcessRemoteRefusedToConnect},
            //};
            //_p2pMethods = new Dictionary<SocketDataType, Action<SocketConnection, string, byte[]>>
            //{
            //    {SocketDataType.Keyboard, ProcessRemoteDataSend },
            //    {SocketDataType.Mouse, ProcessRemoteDataSend },
            //    {SocketDataType.ScreenOk, ProcessRemoteDataSend },
            //    {SocketDataType.ChunksOk, ProcessRemoteDataSend },
            //    {SocketDataType.Clipboard, ProcessRemoteDataSend },
            //    {SocketDataType.Chat, ProcessRemoteDataSend },
            //    {SocketDataType.Screen, ProcessRemoteDataSend },
            //    {SocketDataType.Chunks, ProcessRemoteDataSend },
            //};
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
                SocketDataType.P2PDataSend => ProcessP2PDataTransfer(sender, e.SocketId, e.DataOffset, e.DataLength),
            };
            if (status)
            {

            }
            else
            {

            }
        }

        private bool ProcessP2PDataTransfer(object sender, string remoteConnectionId, int offset, int length)
        {
            if (sender is SocketConnection socketSender)
            {
                try
                {
                    if(_remoteControlManager.GetPartner(remoteConnectionId, socketSender, out SocketConnection socketReceive))
                    {
                        _remoteControlManager.Send(socketReceive, offset, length, false);
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
                        _remoteControlManager.Send(remoteConnection.Controller, offset, length, true);
                        return true;
                    }
                    else
                    {
                        _remoteControlManager.RemoveRemoteConnection(remoteConnectionId);
                    }
                    byte[] packet = PacketFactory.CreatePacket(SocketDataType.P2PConnectFailed, remoteConnectionId);
                    _remoteControlManager.Send(controlled, packet);
                    _remoteControlManager.CloseConnection(controlled);
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
                            _remoteControlManager.Send(validConnection.SocketConnection, data, false);
                            return true;
                        }
                    }
                    byte[] packet = PacketFactory.CreatePacket(SocketDataType.P2PConnectFailed, connectionId);
                    _remoteControlManager.Send(controller, packet);
                    _remoteControlManager.CloseConnection(controller);
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
        //#region Methods
        //public void InitServer()
        //{
        //    try
        //    {
        //        _server.Init();
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.ForContext("FileName", this.GetType().Name).Error(ex, "InitServer Failed");
        //    }
        //}
        //public void StartServer(IPEndPoint ep = null)
        //{
        //    try
        //    {
        //        if (ep == null)
        //        {
        //            ep = new IPEndPoint(IPAddress.Any, DefaultValue.Common.DEFAULT_PORT);
        //        }
        //        _server.Start(ep);
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.ForContext("FileName", this.GetType().Name).Error(ex, "StartServer Failed");
        //    }
        //}
      
        //private void ProcessRemoteAcceptedToConnect(SocketConnection connection, int dataOffset, int dataLength)
        //{
        //    var (type, id, data) = ParseSystemData(connection, dataOffset, dataLength);
        //    try
        //    {
        //        if (type != SocketDataType.None && !string.IsNullOrEmpty(id) && data != null)
        //        {
        //            if (_remoteConnectionManager.AddControlled(id, connection, out var remoteConnection))
        //            {
        //                Send(remoteConnection.Controller, data);
        //                return;
        //            }
        //        }
        //        byte[] packet = PacketFactory.CreatePacket(SocketDataType.P2PConnectFailed, id);
        //        Send(connection, packet);
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.ForContext("FileName", this.GetType().Name).Error(ex, $"ProcessRemoteAcceptedToConnect error on IP: {connection.IP} - Id: {id}");
        //    }
        //}
        ////Note: using the same logged connection to refused
        //private void ProcessRemoteRefusedToConnect(SocketConnection connection, int dataOffset, int dataLength)
        //{
        //    var (type, id, data) = ParseSystemData(connection, dataOffset, dataLength);
        //    try
        //    {
        //        if (type != SocketDataType.None && !string.IsNullOrEmpty(id) && data != null)
        //        {
        //            if (_remoteConnectionManager.TakeAndRemote(id, out var remoteConnection))
        //            {
        //                Send(remoteConnection.Controller, data);
        //                return;
        //            }
        //        }
        //        byte[] packet = PacketFactory.CreatePacket(SocketDataType.Error, id);
        //        Send(connection, packet);
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.ForContext("FileName", this.GetType().Name).Error(ex, $"ProcessRemoteRefusedToConnect error on IP: {connection.IP} - Id: {id}");
        //    }
        //}
        //private void ProcessSocketData(SocketConnection connection, ServerEventArg e)
        //{
        //    try
        //    {
        //        if (e.Offset < 0)
        //        {
        //            Log.ForContext("FileName", this.GetType().Name).Error(new ArgumentNullException(nameof(e.Offset)), "ProcessSocketData error");
        //            return;
        //        }
        //        if (e.Length < 0)
        //        {
        //            Log.ForContext("FileName", this.GetType().Name).Error(new ArgumentNullException(nameof(e.Length)), "ProcessSocketData error");
        //            return;
        //        }
        //        if (connection.SAEA == null || connection.SAEA.Buffer == null)
        //        {
        //            Log.ForContext("FileName", this.GetType().Name).Error(new ArgumentNullException(nameof(connection.SAEA)), "ProcessSocketData error");
        //            return;
        //        }
        //        SocketDataType type = (SocketDataType)connection.SAEA.Buffer[e.Offset + PACKET_TYPE_INDEX];
        //        //Direct to specific method by SocketDataType
        //        if (_systemMethods.ContainsKey(type))
        //        {
        //            if (_systemMethods.TryGetValue(type, out var sysMethod))
        //            {
        //                sysMethod(connection, e.Offset, e.Length);
        //            }
        //        }
        //        else
        //        {
        //            Log.ForContext("FileName", this.GetType().Name).Error("ProcessSocketData Invalid data type");
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.ForContext("FileName", this.GetType().Name).Error(ex, $"ProcessSocketData error on IP: {connection.IP}");
        //    }
        //}
        //private (SocketDataType type, string id, byte[] data) ParseSystemData(SocketConnection connection, int dataOffset, int dataLength)
        //{
        //    try
        //    {
        //        var buffer = connection.SAEA.Buffer;
        //        int offset = dataOffset + PACKET_SIZE_INDEX;
        //        int payloadLength = dataLength - PACKET_HEADER_LENGTH;

        //        byte[] data = new byte[payloadLength];

        //        //Packet size
        //        int packetSize = BitConverter.ToInt32(buffer, offset);
        //        offset += PACKET_SIZE_LENGTH;
        //        if (packetSize != dataLength)
        //        {
        //            Log.ForContext("FileName", this.GetType().Name).Error(new ArgumentException(nameof(dataLength)), "Missing some data");
        //            return (SocketDataType.None, null, null);
        //        }

        //        //Packet type
        //        SocketDataType type = (SocketDataType)buffer[offset];
        //        offset += PACKET_TYPE_LENGTH;

        //        //Id
        //        string id = Encoding.ASCII.ByteArrayToString(buffer, offset, PACKET_ID_LENGTH);
        //        offset += PACKET_ID_LENGTH;

        //        //Payload
        //        Buffer.BlockCopy(buffer, offset, data, 0, payloadLength);

        //        return (type, id, data);
        //    }
        //    catch
        //    {
        //        return (SocketDataType.None, null, null);
        //    }
        //}
        //private void ProcessConnect(SocketConnection connection, int dataOffset, int dataLength)
        //{
        //    //TODO
        //}
        //private void ProcessLogin(SocketConnection connection, int dataOffset, int dataLength)
        //{
        //    try
        //    {
        //        var (type, id, data) = ParseSystemData(connection, dataOffset, dataLength);

        //        if (_socketConnectionManager.NewConnectionInfo(data, connection, out var connectionInfo))
        //        {
        //            ProcessLoginSucceeded(connection, connectionInfo);
        //            Log.ForContext("FileName", this.GetType().Name).Information($"Login success on IP: {connection.IP}");
        //        }
        //        else
        //        {
        //            ProcessLoginFailed(connection);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.ForContext("FileName", this.GetType().Name).Error(ex, "Login error");
        //    }
        //}
        //private void ProcessLoginSucceeded(SocketConnection connection, ConnectionInfo connectionInfo)
        //{
        //    try
        //    {
        //        byte[] data = Encoding.ASCII.StringToByteArray(connectionInfo.ToNetworkString());
        //        byte[] packet = PacketFactory.CreatePacket(SocketDataType.Login, connectionInfo.Id, data);
        //        Send(connection, packet);
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.ForContext("FileName", this.GetType().Name).Error(ex, "ProcessLoginFailed error");
        //    }
        //}
        //private void ProcessLoginFailed(SocketConnection connection)
        //{
        //    try
        //    {
        //        byte[] packet = PacketFactory.CreatePacket(SocketDataType.LoginFailed, EMPTY_ID);
        //        Send(connection, packet);
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.ForContext("FileName", this.GetType().Name).Error(ex, "ProcessLoginFailed error");
        //    }
        //}
        //private void ProcessRemoteDataSend(SocketConnection connection, string id, byte[] data)
        //{
        //    try
        //    {
        //        if (_remoteConnectionManager.GetPartner(connection, out var partner))
        //        {
        //            Send(partner, data);
        //        }
        //        else
        //        {
        //            byte[] packet = PacketFactory.CreatePacket(SocketDataType.P2PDataSendError, id);
        //            Send(connection, packet);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.ForContext("FileName", this.GetType().Name).Error(ex, $"ProcessRemoteAcceptedToConnect error on IP: {connection.IP} - Id: {id}");
        //    }
        //}
        //private void CloseConnection(SocketConnection connection)
        //{
        //    try
        //    {
        //        _server.Close(connection);
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.ForContext("FileName", this.GetType().Name).Error(ex, "CloseConnection error");
        //    }
        //}
        //private void Send(SocketConnection connection, byte[] data)
        //{
        //    try
        //    {
        //        _server.Send(connection, data);
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.ForContext("FileName", this.GetType().Name).Error(ex, "RemoteSend error");
        //    }
        //}
        //private void RemoteSend(SocketConnection connection, byte[] data)
        //{
        //    try
        //    {
        //        var partner = _remoteConnectionManager.GetPartner(connection);
        //        if (partner == null)
        //        {
        //            Log.ForContext("FileName", this.GetType().Name).Error(new InvalidOperationException(nameof(partner)), "Cannot found partner");
        //            return;
        //        }
        //        Send(partner, data);
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.ForContext("FileName", this.GetType().Name).Error(ex, "RemoteSend error");
        //    }
        //}
        //#endregion
        //#region Events
        //private void ServerEventHandler(object sender, ServerEventArg e)
        //{
        //    if(sender is SocketConnection connection)
        //    {
        //        ProcessSocketData(connection, e);
        //    }
        //}
        //private void SocketConnectionManagerEventHandler(object sender, SocketConnectionManagerEventArg e)
        //{
        //    Console.WriteLine($"{this.GetType().Name} - SocketConnectionManagerEventHandler Called - Type:{e.Type}");
        //}
        //private void RemoteSocketManagerEventHandler(object sender, RemoteConnectionEventArg e)
        //{
        //    Console.WriteLine($"{this.GetType().Name} - RemoteSocketManagerEventHandler Called");
        //}
        //#endregion
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
