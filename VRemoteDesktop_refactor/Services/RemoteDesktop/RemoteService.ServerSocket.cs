using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vsign4.VRemoteDesktop.DTOs;
using Vsign4.VRemoteDesktop.DTOs.Requests;
using Vsign4.VRemoteDesktop.Helpers;
using Vsign4.VRemoteDesktop.Services.RemoteDesktop.DTOs;
using Vsign4.VRemoteDesktop.Services.RemoteDesktop.Enums;
using Vsign4.VRemoteDesktop.Services.RemoteDesktop.Events;
using Vsign4.VRemoteDesktop.Services.SessionManagement;
using Vsign4.VRemoteDesktop.Services.SessionManagement.DTOs;
using Vsign4.VRemoteDesktop.Services.SessionManagement.Enums;
using Vsign4.VRemoteDesktop.Services.SessionManagement.Events.ClientSession;
using Vsign4.VRemoteDesktop.Utils;

namespace Vsign4.VRemoteDesktop.Services.RemoteDesktop
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
                sessionId = StringHelper.RandomString(HeaderSchema.SessionIdLength);

            return _sessionManager.New(sessionId,
                SessionManagement.Enums.ClientType.System,
                _machineProfile.Bounds.Width,
                _machineProfile.Bounds.Height,
                BYTE_PER_PIXEL,
                PIXEL_FORMAT);
        }
        public bool ConnectToServer(ClientSession serverSocket, string ip, int port)
        {
            if (string.IsNullOrEmpty(ip))
                throw new ArgumentNullException("Server ip cannot be null or empty");
            if (port <= 0)
                throw new ArgumentOutOfRangeException("Server port cannot less than or equal zero");

            return serverSocket.TryConnect(ip, port, 3, 3000);
        }
        public void ServerSocketLogin(ClientSession clientSession)
        {
            if (clientSession == null)
                throw new ArgumentNullException("ClientSession cannot be null");

            var machineInfo = GetMachineInfo().ObjectToJsonString();
            var result = ByteArrayHelper.ConvertStringToByteArray(machineInfo, VRemoteDesktop.Enums.EncodingType.ASCII);
            if (result.IsSuccess)
            {
                clientSession.AddWork(QueuePriority.High, new TaskObject(SocketDataType.Login, result.Data));
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
        public void GetPartnerInfo(ClientSession clientSession, string id, string password)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentNullException("Partner id cannot be null or empty");
            if (string.IsNullOrEmpty(password))
                throw new ArgumentNullException("Partner password cannot be null or empty");

            var partnerCredentials = new PartnerCredentials(id, password);

            var packet = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(partnerCredentials));

            clientSession.AddWork(QueuePriority.High, new TaskObject(SocketDataType.GetPartnerInfo, packet));

        }
        #endregion
        #region Events
        private void LoginEventHandler(object sender, ClientSessionDataReceivedEventArgs e)
        {
            var clientSession = sender as ClientSession;
            if (clientSession != null)
            {
                try
                {
                    string dataString = Encoding.ASCII.GetString(e.Data);
                    var loginResponse = JsonConvert.DeserializeObject<LoginResponse>(dataString);
                    if (loginResponse != null && loginResponse.IsSuccess)
                    {
                        UpdatePublicIp(loginResponse.PublicIP);

                        if(OnSessionData != null)
                            OnSessionData.Invoke(sender, new RemoteDesktopEventArgs(Enums.ResponseType.LoginSuccess, clientSession.SessionId));
                    }
                    else
                    {
                        if (OnSessionData != null)
                            OnSessionData.Invoke(sender, new RemoteDesktopEventArgs(Enums.ResponseType.LoginFailed, clientSession.SessionId));
                    }
                }
                catch (Exception ex)
                {
                    // Was rethrowing on a background socket callback thread (could crash the
                    // process and dropped the log). Log and surface a login failure instead.
                    Logger.Log.ForContext("FileName", GetType().Name).Error(ex, "LoginEventHandler error");
                    if (OnSessionData != null)
                        OnSessionData.Invoke(sender, new RemoteDesktopEventArgs(Enums.ResponseType.LoginFailed, clientSession.SessionId));
                }
            }
        }

        private void ConnectCallback(object sender, ClientSessionDataReceivedEventArgs e)
        {
            ClientSession session = sender as ClientSession;    
            if (session != null)
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
            if (clientSession != null)
            {
                var msg = Encoding.ASCII.GetString(data);

                if(OnSessionData != null)   
                    OnSessionData.Invoke(this, new RemoteDesktopEventArgs(ResponseType.GetPartnerInfoFailed, clientSession.SessionId, message: msg));
            }
        }
        #endregion
        #endregion
        #endregion
    }
}
