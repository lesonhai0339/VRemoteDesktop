using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Services.RemoteDesktop;
using VRemoteDesktop.Services.ScreenCapture.DTOs;
using VRemoteDesktop.Services.SessionManagement.Enums;
using VRemoteDesktop.Services.SessionManagement.Events.ClientSession;
using VRemoteDesktop.Services.VTCPClient;
using VRemoteDesktop.Services.VTCPClient.Events;

namespace VRemoteDesktop.Services.SessionManagement
{
    public class SessionManager: IDisposable
    {
        private bool _disposed = false;
        private readonly ConcurrentDictionary<string, ClientSession> _sessions;
        public EventHandler<ClientSessionDataReceivedEventArgs> SessionDataReceived;
        public EventHandler<ClientSessionDisconnectedEventArgs> SessionClosed;
        public SessionManager()
        {
            _sessions = new ConcurrentDictionary<string, ClientSession>();
        }
        public ConcurrentDictionary<string, ClientSession> Connections => _sessions;
        #region Manager
        public bool Find(string id)
        {
            return _sessions.Values.Any(session
                => session.PartnerInfo != null 
                && !string.IsNullOrEmpty(session.PartnerInfo.Id) 
                && session.PartnerInfo.Id.Equals(id, StringComparison.OrdinalIgnoreCase));    
        }
        public bool HasClientOfType(ClientType type)
        {
            foreach (var connection in _sessions)
            {
                if (connection.Value.SessionType == type)
                    return true;
            }
            return false;
        }
        public void Add(string id, ClientSession session)
        {
            if (!_sessions.TryAdd(id, session))
            {
                (session as IDisposable)?.Dispose();
                throw new InvalidOperationException(string.Format("Client with Id:{0} already exists", id));
            }
            if (session != null)
            {
                session.OnDataReceived += OnSessionDataReceivedEvetHandler;
                session.OnDisconnected += OnSessionDisconnectedEventHandler;
            }
        }

        private void OnSessionDataReceivedEvetHandler(object sender, ClientSessionDataReceivedEventArgs e)
        {
            if (SessionDataReceived != null)
                SessionDataReceived.Invoke(sender, new ClientSessionDataReceivedEventArgs(e.SessionId, type: e.Type, data: e.Data, e.IsSuccess));
        }

        private void OnSessionDisconnectedEventHandler(object sender, ClientSessionDisconnectedEventArgs e)
        {
            var handler = SessionClosed;
            if (handler != null)
                handler.Invoke(sender, e);
        }
        public bool Remove(string id)
        {
            if (_sessions.TryRemove(id, out var client))
            {
                client.OnDataReceived -= OnSessionDataReceivedEvetHandler;
                client.OnDisconnected -= OnSessionDisconnectedEventHandler;
                (client as IDisposable)?.Dispose();
                return true;
            }
            throw new InvalidOperationException(string.Format("Cannot remove connection with Id:{0}", id));
        }

        public bool Remove(ClientSession session)
        {
            if (session != null)
            {
                session.OnDataReceived -= OnSessionDataReceivedEvetHandler;
                session.OnDisconnected -= OnSessionDisconnectedEventHandler;
                if (_sessions.TryRemove(session.SessionId, out _))
                {
                    return true;
                }
            }
            throw new InvalidOperationException(string.Format("Cannot remove connection with Id:{0}", session.SessionId));
        }
        public ClientSession GetByKey(string id)
        {
            if (_sessions.TryGetValue(id, out var session))
            {
                return session;
            }
            throw new InvalidOperationException(string.Format("Connection with Id:{0} does not exists", id));
        }
        public ClientSession New(string id, ClientType type)
        {
            if (_sessions.TryGetValue(id, out var existed))
            {
                return existed;
            }

            ClientSession session = new ClientSession(id, type);
            Add(id, session);
            return session;
        }
        public ClientSession AddNewAndListen(string id, ClientType type, int port)
        {
            if (_sessions.TryGetValue(id, out var existed))
            {
                return existed;
            }

            ClientSession client = new ClientSession(id, type);
            Add(id, client);

            bool result = client.Listen(port);
            if (!result)
            {
                Remove(id);
                throw new InvalidOperationException(string.Format("Cannot start listening for client with Id:{0}", id));
            }
            return client;
        }
        #endregion
        #region Dispatch
        public void AddScreen(ClientType type, RegionFrame screen)
        {
            if (type == ClientType.None) return;
            if (screen == null)
                throw new ArgumentNullException("Full screen cannot be null");

            var sessions = _sessions.Where(x => x.Value.SessionType == type && x.Value.FullScreenReceived()).Select(x => x.Value).ToList();
            foreach(var session in sessions)
            {
                AddScreen(session, screen);
            }
        }
        private void AddScreen(ClientSession session, RegionFrame screen)
        {
            session.AddScreen(screen);
        }
        public void AddDirtyRegions(ClientType sessionType, RegionFrame frame)
        {
            if (sessionType == ClientType.None) return;
            if (frame == null) throw new ArgumentNullException("Dirty region cannot be null");

            var sessions = _sessions.Where(x => x.Value.SessionType == sessionType && x.Value.AcceptScreen).Select(x => x.Value).ToList();
            foreach(var session in sessions)
            {
                AddRegions(session, frame);
            }
        }
        private void AddRegions(ClientSession session, RegionFrame frame)
        {
            session.AddRegions(frame);
        }
        #endregion
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_disposed) return;
                foreach (var connection in _sessions)
                {
                    connection.Value?.Dispose();
                }
                _sessions.Clear();
                _disposed = true;
            }
        }
    }
}
