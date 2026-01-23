using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Services.SessionManagement.Enums;
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
        #endregion
        #region Events
        private void EventReceived(object sender, RemoteDesktopEventArgs e)
        {
            if (_eventHandlers.TryGetValue(e.Type, out var handle))
            {
                try
                {
                    handle(sender, e);
                }
                catch (Exception ex)
                {
                    Logger.Log.ForContext("FileName", GetType().Name).Error(ex, string.Format("Error handling {0}: {1}", e.Type, ex.Message));
                }
            };
        }
        #endregion
        #endregion
        #endregion
    }
}
