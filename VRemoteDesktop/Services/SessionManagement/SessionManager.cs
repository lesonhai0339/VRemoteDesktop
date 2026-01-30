using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading;
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
    public interface ISessionManager
    {
        ClientSession[] Connections { get; }
        bool Contains(string sessionId);
        bool Contains(Func<ClientSession, bool> predicate);
        bool HasClientOfType(ClientType type);
        void Add(string id, ClientSession session);
        bool Remove(string id);
        bool Remove(ClientSession session);
        ClientSession GetByKey(string id);
        ClientSession New(string id, ClientType type, int width, int height);
        ClientSession AddNewAndListen(string id, ClientType type, int port, int width, int height);

        void AddScreen(ClientType type, RegionFrame screen);
        void AddDirtyRegions(ClientType sessionType, RegionFrame frame);

        event EventHandler<ClientSessionDataReceivedEventArgs> SessionDataReceived;
        event EventHandler<ClientSessionDisconnectedEventArgs> SessionClosed;

        void Dispose();
    }
    public class SessionManager: ISessionManager, IDisposable
    {
        private int _disposed = 0;
        private readonly ConcurrentDictionary<string, ClientSession> _sessions;
        public event EventHandler<ClientSessionDataReceivedEventArgs> SessionDataReceived;
        public event EventHandler<ClientSessionDisconnectedEventArgs> SessionClosed;
        public SessionManager()
        {
            _sessions = new ConcurrentDictionary<string, ClientSession>();
        }
        public ClientSession[] Connections => _sessions.Values.ToArray();
        #region Manager
        public bool Contains(string sessionId)
        {
            return _sessions.ContainsKey(sessionId);
        }
        public bool Contains(Func<ClientSession, bool> predicate)
        {
            return _sessions.Values.Any(predicate);    
        }
        public bool HasClientOfType(ClientType type)
        {
            return _sessions.Values.Any(x => x != null && x.SessionType == type);
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
        public ClientSession New(string id, ClientType type, int width, int height)
        {
            if (_sessions.TryGetValue(id, out var existed))
            {
                return existed;
            }

            ClientSession session = new ClientSession(id, type, width, height);
            Add(id, session);
            return session;
        }
        public ClientSession AddNewAndListen(string id, ClientType type, int port, int width, int height)
        {
            if (_sessions.TryGetValue(id, out var existed))
            {
                return existed;
            }

            ClientSession session = new ClientSession(id, type, width, height);
            Add(id, session);

            bool result = session.Listen(port);
            if (!result)
            {
                Remove(id);
                throw new InvalidOperationException(string.Format("Cannot start listening for client with Id:{0}", id));
            }
            return session;
        }
        #endregion
        #region Dispatch
        public void AddScreen(ClientType type, RegionFrame screen)
        {
            if (type == ClientType.None) return;
            if (screen == null)
                throw new ArgumentNullException("Full screen cannot be null");

            var sessions = _sessions.Where(x => x.Value.SessionType == type && x.Value.AcceptFullScreen()).Select(x => x.Value).ToList();
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
        #region Events
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

            try
            {
                if (disposing)
                {
                    foreach (var key in _sessions.Keys)
                    {
                        try
                        {
                            Remove(key);
                        }
                        catch { /*Err*/ }
                    }
                    _sessions.Clear();
                }
            }
            catch { }
        }
    }
}
