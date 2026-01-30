using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.DTOs.Requests;
using VRemoteDesktop.Events;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.RemoteDesktop.DTOs;
using VRemoteDesktop.Services.RemoteDesktop.Enums;
using VRemoteDesktop.Services.SessionManagement.Enums;
using VRemoteDesktop.Services.SessionManagement.Events.ClientSession;

namespace VRemoteDesktop.Services.RemoteDesktop
{
    public partial class RemoteService
    {
        #region ClientSession
        #region Server
        #region Properties
        #endregion
        #region Methods
        public ClientSession NewSocketServer(string sessionId = null)
        {
            if (string.IsNullOrEmpty(sessionId))
                sessionId = StringHelper.RandomString(SESSION_ID_LENGTH);

            return _sessionManager.New(sessionId, 
                SessionManagement.Enums.ClientType.System, 
                _machineProfile.Bounds.Width, 
                _machineProfile.Bounds.Height,
                BYTE_PER_PIXEL,
                PIXEL_FORMAT);
        }
        public  bool ConnectToServer(ClientSession serverSocket, string ip, int port)
        {
            if (string.IsNullOrEmpty(ip))
                throw new ArgumentNullException("Server ip cannot be null or empty");
            if (port <= 0)
                throw new ArgumentOutOfRangeException("Server port cannot less than or equal zero");

            return serverSocket.TryConnect(ip, port, 3, 3000);
        }
        public void ServerSocketLogin(ClientSession client)
        {
            if (client == null)
                throw new ArgumentNullException("ClientSession cannot be null");

            var machineInfo = GetMachineInfo().ObjectToJsonString();
            var result = ByteArrayHelper.ConvertStringToByteArray(machineInfo, VRemoteDesktop.Enums.EncodingType.ASCII);
            if (result.IsSuccess)
            {
                client.Send(Models.SocketDataType.Login, result.Data);
            }
        }
        public void ServerSocketListen(ClientSession client, int port)
        {
            if (client == null)
                throw new ArgumentNullException("Client session cannot be null");
            if (port <= 0)
                throw new ArgumentOutOfRangeException("Port cannot less than or equal zero");

            client.Listen(port: port);
        }
        public void GetPartnerInfo(ClientSession client, string id, string password)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentNullException("Partner id cannot be null or empty");
            if (string.IsNullOrEmpty(password))
                throw new ArgumentNullException("Partner password cannot be null or empty");

            var partnerCredentials = new PartnerCredentials(id, password);

            var packet = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(partnerCredentials));

            client.Send(SocketDataType.GetPartnerInfo, packet);
        }
        #endregion
        #region Events
        private void LoginEventHandler(object sender, ClientSessionDataReceivedEventArgs e)
        {
            var clientSession = sender as ClientSession;
            if(clientSession != null)
            {
                try
                {
                    string dataString = Encoding.ASCII.GetString(e.Data);
                    var loginResponse = JsonConvert.DeserializeObject<LoginResponse>(dataString);
                    if (loginResponse != null && loginResponse.IsSuccess)
                    {
                        UpdatePublicIp(loginResponse.PublicIP);
                        OnSessionData?.Invoke(sender, new RemoteDesktopEventArgs(Enums.ResponseType.LoginSuccess, clientSession.SessionId));
                    }
                    else
                    {
                        OnSessionData?.Invoke(sender, new RemoteDesktopEventArgs(Enums.ResponseType.LoginFailed , clientSession.SessionId));
                    }
                }
                catch (Exception ex)
                {
                    throw;
                }
            }          
        }
       
        private void ConnectCallback(object sender, ClientSessionDataReceivedEventArgs e)
        {
            if (sender is ClientSession session)
            {
                if (session.SessionType == ClientType.System)
                {
                    ServerSocketConnectEventHandler(session, e);
                }
                else
                {
                    ClientSocketConnectEventHandler(session, e);
                }
            }
        }
        private void ServerSocketConnectEventHandler(ClientSession session, ClientSessionDataReceivedEventArgs e)
        {
            var response = e.IsSuccess ? ResponseType.ConnectSuccess : ResponseType.ConnectFailed;
            var handler = OnSessionData;
            if (handler != null)
                handler.Invoke(session, new RemoteDesktopEventArgs(response, session.SessionId));
        }
        private void GetPartnerInfoFailedCallback(object sender, byte[] data)
        {
            var clientSession = sender as ClientSession;
            if(clientSession != null)
            {
                var msg = Encoding.ASCII.GetString(data);
                OnSessionData?.Invoke(this, new RemoteDesktopEventArgs(ResponseType.GetPartnerInfoFailed, clientSession.SessionId ,  message: msg));
            }
        }
        #endregion
        #endregion
        #endregion
    }
}
