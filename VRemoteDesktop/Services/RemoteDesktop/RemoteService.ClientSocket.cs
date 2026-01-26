using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        #region ClientSession
        #region Client
        #region Properties
        #endregion
        #region Methods
        public ClientSession NewControlled(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                sessionId = StringHelper.RandomStringNumber(SESSION_ID_LENGTH);

            var session = _sessionManager.New(sessionId, ClientType.Controlled);

            //Note*** do something later
            //if (_sessionManager.Connections.Count > 0)
            //{
            //    if (_sessionManager.HasClientOfType(ClientType.Controlled))
            //        StartScreenCapture();
            //}


            return session;
        }
        public ClientSession NewController(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                sessionId = StringHelper.RandomStringNumber(SESSION_ID_LENGTH);

            var session = _sessionManager.New(sessionId, ClientType.Controller);

            return session;
        }
        public ClientSession GetClientById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            var session = _sessionManager.GetByKey(id);
            return session;
        }
        public bool FindClient(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException("Session Id cannot be null or empty");
            return _sessionManager.Find(id);
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
            var dataString = Encoding.ASCII.GetString(data);
            var partnerNetworkInfo = JsonConvert.DeserializeObject<PartnerNetworkInfo>(dataString);
            if (partnerNetworkInfo == null)
                //TODo
                return;

            var clientSession = NewController(partnerNetworkInfo.SessionId);
            if (clientSession == null)
                //TODO
                return;

            string connectIP = _machineProfile.SameNetwork(partnerNetworkInfo.PublicIP) 
                ? partnerNetworkInfo.LocalIP 
                : partnerNetworkInfo.PublicIP;

            bool isSuccess = clientSession.TryConnect(connectIP, int.Parse(DEFAULT_REMOTE_PORT), 0, 3000);

        }
        private void CreateRemoteConnection(byte[] data)
        {
            var dataString = Encoding.ASCII.GetString(data);
            var partnerNetworkInfo = JsonConvert.DeserializeObject<PartnerNetworkInfo>(dataString);
            if (partnerNetworkInfo == null)
                //TODo
                return;

            var clientSession = NewControlled(partnerNetworkInfo.SessionId);
            if (clientSession == null)
                //TODO
                return;

            bool isSuccess = clientSession.Listen(int.Parse(DEFAULT_REMOTE_PORT), 3000);

        }
        #endregion
        #region Events


        private void ClientSocketConnectEventHandler(ClientSession session, ClientSessionDataReceivedEventArgs e)
        {
            throw new NotImplementedException();
        }
        private void ClientSessionClosedEventHandler(object sender, EventArgs e)
        {
            var clientSession = sender as ClientSession;
            if (clientSession != null)
            {
                try
                {
                    //clientSession.IsP2PConnected = false;

                    if (clientSession.SessionType == ClientType.System)
                    {
                        OnSessionData?.Invoke(sender, new RemoteDesktopEventArgs(Enums.ResponseType.Disconnect, false, new byte[0]));
                    }

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
        #endregion
        #endregion
        #endregion
    }
}
