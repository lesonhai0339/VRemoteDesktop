using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Threading;
using VRemoteDesktop.DTOs.Requests;
using VRemoteDesktop.DTOs.Response;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.SessionManagement.Enums;
using VRemoteDesktop.Services.SessionManagement.Events.ClientSession;
using VRemoteDesktop.Utils;

namespace VRemoteDesktop.Services.RemoteDesktop
{
    public partial class RemoteService
    {
        #region Methods
        public ClientSession NewControlled(string sessionId, int width, int height)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                sessionId = StringHelper.RandomStringNumber(SESSION_ID_LENGTH);

            var session = _sessionManager.New(id: sessionId,type: ClientType.Controlled, width: width, height: height, bytePerPixel: BYTE_PER_PIXEL, pixelFormat: PIXEL_FORMAT);

            if (_sessionManager.HasClientOfType(ClientType.Controlled))
            {
                InitCapturingBuffer();
            }

            return session;
        }
        public ClientSession NewController(string sessionId, int width, int height, PixelFormat pixelFormat)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                sessionId = StringHelper.RandomStringNumber(SESSION_ID_LENGTH);

            var session = _sessionManager.New(id: sessionId, type: ClientType.Controlled, width: width, height: height, bytePerPixel: BYTE_PER_PIXEL, pixelFormat: pixelFormat);

            return session;
        }
        public ClientSession GetClientById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            var session = _sessionManager.GetByKey(id);
            return session;
        }
        public bool FindClient(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentNullException("Session Id cannot be null or empty");
            return _sessionManager.Contains(sessionId);
        }
        public void Listen(string sessionId, int port)
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentNullException("SessionId cannot be null or empty");
            if (port <= 0) throw new ArgumentOutOfRangeException("Port cannot less than or equal zero");

            var client = _sessionManager.GetByKey(sessionId);
            if (client == null)
                throw new InvalidOperationException(string.Format("Client with session id {0} does not exists", sessionId));

            client.Listen(port);    
        }
        public void RemoveById(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentNullException("Session id cannot be null or empty");

            if (!_sessionManager.Remove(sessionId))
                throw new InvalidOperationException(string.Format("Client with id {0} does not exists", sessionId));

            //Stop capturing here, *****
        }
        private void GetPartnerInfoSuccessCallback(byte[] data)
        {
            var partnerNetworkInfo = JsonConvert.DeserializeObject<PartnerNetworkInfo>(Encoding.ASCII.GetString(data));
            if (partnerNetworkInfo == null)
                //TODo
                return;

            var clientSession = NewController(partnerNetworkInfo.SessionId, 
                _machineProfile.Bounds.Width,
                _machineProfile.Bounds.Height,
                PIXEL_FORMAT);

            if (clientSession == null)
                //TODO
                return;

            clientSession.UpdatePartnerInfo(partnerNetworkInfo);

            string connectIP = _machineProfile.SameNetwork(partnerNetworkInfo.PublicIP) 
                ? partnerNetworkInfo.LocalIP 
                : partnerNetworkInfo.PublicIP;

            bool isSuccess = clientSession.TryConnect(connectIP, int.Parse(DEFAULT_REMOTE_PORT), RETRY, TIMEOUT);

            if (isSuccess)
            {
                EstablishPeer2PeerConnect(clientSession, partnerNetworkInfo);
            }
            else
            {
                EstablishRelayConnect(clientSession, partnerNetworkInfo);
            }
        }  /// <summary>
           /// Received request to connect packet from server, new clientsession and listen on remote port. If p2p failed  connect trying to connect
           /// to server and wait  controller send login Info
           /// </summary>
           /// <param name="data"></param>
        private void CreateRemoteConnection(object sender, byte[] data)
        {
            var dataString = Encoding.ASCII.GetString(data);
            var partnerNetworkInfo = JsonConvert.DeserializeObject<PartnerNetworkInfo>(dataString);
            if (partnerNetworkInfo == null)
                //TODo
                return;

            SendAck(sender, Encoding.ASCII.GetBytes(partnerNetworkInfo.SessionId), SocketDataType.P2PReady);

            var clientSession = NewControlled(partnerNetworkInfo.SessionId, 
                _machineProfile.Bounds.Width,
                _machineProfile.Bounds.Height);
            if (clientSession == null)
                //TODO
                return;

            clientSession.UpdatePartnerInfo(partnerNetworkInfo);


            bool isSuccess = clientSession.Listen(int.Parse(DEFAULT_REMOTE_PORT), TIMEOUT + 1);

            if (!isSuccess)
            {
                bool connectSuccess = clientSession.TryConnect(DEFAULT_SERVER_IP, int.Parse(DEFAULT_REMOTE_PORT), RETRY, TIMEOUT);
                if (!connectSuccess)
                {
                    OnError?.Invoke(this, new Events.RemoteDesktopErrorEventArgs(clientSession.SessionId, "Connect to server failed")); //Failed
                }

                var connectionInfo = new ConnectionCredentials(
                   partnerId: partnerNetworkInfo.PartnerId,
                   partnerPassword: partnerNetworkInfo.PartnerPassword,
                   type: ControlType.Controlled,
                   machineInfo: _machineProfile.MachineInfo);

                var packet = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(connectionInfo));

                //clientSession.Send(SocketDataType.RemoteLogin, packet);
                clientSession.AddWork(priority: QueuePriority.High, new TaskObject( SocketDataType.RemoteLogin, packet));
            }
        }
        private void EstablishRelayConnect(ClientSession clientSession, PartnerNetworkInfo partnerInfo)
        {
            bool isSuccess = clientSession.TryConnect(DEFAULT_SERVER_IP, int.Parse(DEFAULT_REMOTE_PORT), RETRY, TIMEOUT);
            if (!isSuccess)
            {
                OnError?.Invoke(this, new Events.RemoteDesktopErrorEventArgs(clientSession.SessionId, "Connect to server failed")); //Connect to TURN server failed, throw back to UI
            }
            Console.WriteLine("Connect to relay server success");
            //Connect success
            EstablishPeer2PeerConnect(clientSession, partnerInfo);
        }
        private void EstablishPeer2PeerConnect(ClientSession clientSession, PartnerNetworkInfo partnerInfo)
        {
            var connectionInfo = new ConnectionCredentials(
                partnerId: partnerInfo.PartnerId,
                partnerPassword: partnerInfo.PartnerPassword,
                type: ControlType.Controller,
                machineInfo: _machineProfile.MachineInfo);

            var packet = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(connectionInfo));
            clientSession.AddWork(priority: QueuePriority.High, new TaskObject(SocketDataType.RemoteLogin, packet));
            //clientSession.Send(SocketDataType.RemoteLogin, packet);
        }
        #endregion
        #region Events
        private void RemoteLoginCallback(object sender, byte[] data)
        {
            try
            {
                var clientSession = sender as ClientSession;
                if (clientSession == null)
                {
                    clientSession.AddWork(priority: QueuePriority.High, new TaskObject(SocketDataType.RemoteLoginFailed));
                    //clientSession.Send(SocketDataType.RemoteLoginFailed, new byte[0]);
                    return;
                }

                var connectionInfo = JsonConvert.DeserializeObject<ConnectionCredentials>(Encoding.ASCII.GetString(data));
                if (connectionInfo == null || string.IsNullOrEmpty(connectionInfo.PartnerId) || string.IsNullOrEmpty(connectionInfo.PartnerPassword))
                {
                    clientSession.AddWork(priority: QueuePriority.High, new TaskObject(SocketDataType.RemoteLoginFailed));
                    //clientSession.Send(SocketDataType.RemoteLoginFailed, new byte[0]);
                    return;
                }

                if (!_machineProfile.Authentication(connectionInfo.PartnerId, connectionInfo.PartnerPassword))
                {
                    clientSession.AddWork(priority: QueuePriority.High, new TaskObject(SocketDataType.RemoteLoginFailed));
                    //clientSession.Send(SocketDataType.RemoteLoginFailed, new byte[0]);
                    return;
                }

                var myInfo = new RemoteLoginResponse(loggedIn: true, clientSession.SessionId, _machineProfile.MachineInfo);

                var packet = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(myInfo));

                //clientSession.Send(SocketDataType.RemoteLoginSuccess, packet);
                clientSession.AddWork(priority: QueuePriority.High, new TaskObject(SocketDataType.RemoteLoginSuccess, packet));


                ReadyToRemoteControlledHandler(sender, null);

            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", "ClientSession").Error(ex, "RemoteLogin err ");
            }
        }

        private void ClientSocketConnectEventHandler(ClientSession session, ClientSessionDataReceivedEventArgs e)
        {
            //Do nothing
        }
        private void PingEventHandler(object sender, ClientSessionDataReceivedEventArgs e)
        {
            var session = sender as ClientSession;
            if (session != null)
                session.AddWork(QueuePriority.High, new TaskObject(SocketDataType.Pong));
        }
        private void ClientSessionClosedEventHandler(object sender, EventArgs e)
        {
            var clientSession = sender as ClientSession;
            if (clientSession != null)
            {
                try
                {
                    //clientSession.IsP2PConnected = false;

                    if (clientSession.SessionType == ClientType.System && OnSessionData != null)
                        OnSessionData?.Invoke(sender, new RemoteDesktopEventArgs(Enums.ResponseType.Disconnect, clientSession.SessionId, false, new byte[0]));

                    if (OnSessionDisconnected != null)
                        OnSessionDisconnected.Invoke(sender, new Events.RemoteDesktopSessionDisconnectEventArgs(clientSession.SessionId));

                    _sessionManager.Remove(clientSession.SessionId);

                    //Unregister received capture
                    if (clientSession.SessionType == ClientType.Controlled)
                    {
                        _screenSender.RemoveSessionBuffer(clientSession.SessionId);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log.ForContext("FileName", GetType().Name).Error(ex, "VClientClosedEventHandler error ");
                }
            }
        }
        private void ReadyToRemoteControllerHandler(object sender, ClientSessionDataReceivedEventArgs e)
        {
            Console.WriteLine("Ready to remote controller");
            var clientSession = sender as ClientSession;
            if(clientSession != null)
            {
                var handler = OnSessionData;
                if (handler != null)
                {
                    clientSession.Connected = true;
                    //Invoke to frmMain to create fromRemote and add client session to frmChat
                    handler.Invoke(sender, new RemoteDesktopEventArgs(Enums.ResponseType.AddRemoteController, clientSession.SessionId));
                }
            }
        }
        private void ReadyToRemoteControlledHandler(object sender, ClientSessionDataReceivedEventArgs e)
        {
            var clientSession = sender as ClientSession;
            if(clientSession != null)
            {
                if (_sessionManager.Contains(clientSession.SessionId))
                {
                    var handler = OnSessionData;
                    if (handler != null)
                    {
                        clientSession.Connected = true;
                        handler.Invoke(sender, new RemoteDesktopEventArgs(Enums.ResponseType.AddRemoteControlled, clientSession.SessionId));
                    }

                    //Register client session with screen capture, get full screen and send to controller
                    _screenSender.AddSessionBuffer(clientSession.SessionId, clientSession.BufferSwapper);
                    _screenSender.GetFullScreen(clientSession.BufferSwapper);

                    StartCapture();

                }
            }
        }
        private void RemoteControlDisconnectHandler(object sender, ClientSessionDataReceivedEventArgs e)
        {
            var clientSession = sender as ClientSession;
            if (clientSession != null)
            {
                _sessionManager.Remove(clientSession.SessionId);

                if(OnSessionData != null)
                {
                    OnSessionData.Invoke(sender, new RemoteDesktopEventArgs(Enums.ResponseType.RemoteDisconnect, clientSession.SessionId));
                }

                _screenSender.RemoveSessionBuffer(clientSession.SessionId);

                StopCapture();
            }
        }
        #endregion
    }
}
