using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading;
using Vsign4.VRemoteDesktop.Services.ScreenCapture.DTOs;
using Vsign4.VRemoteDesktop.Services.ScreenCapture.Enums;
using Vsign4.VRemoteDesktop.Services.SessionManagement.Enums;
using Vsign4.VRemoteDesktop.Services.SessionManagement.Events.ClientSession;

namespace Vsign4.VRemoteDesktop.Services.SessionManagement
{
    public interface ISessionManager
    {
        bool Contains(string sessionId);
        bool Contains(Func<ClientSession, bool> predicate);
        bool HasClientOfType(ClientType type);
        void Add(string id, ClientSession session);
        bool Remove(string id);
        bool Remove(ClientSession session);
        void ScreenCapture(ClientType type, VScreenType e);
        void Dispose();
        ClientSession[] Connections { get; }
        ClientSession GetByKey(string id);
        ClientSession New(string id, ClientType type, int width, int height, int bytePerPixel, PixelFormat pixelFormat);
        ClientSession AddNewAndListen(string id, ClientType type, int port, int width, int height, int bytePerPixel, PixelFormat pixelFormat);

        event EventHandler<ClientSessionDataReceivedEventArgs> SessionDataReceived;
        event EventHandler<ClientSessionDisconnectedEventArgs> SessionClosed;
    }
    public class SessionManager : ISessionManager, IDisposable
    {
        private int _disposed = 0;
        private readonly ConcurrentDictionary<string, ClientSession> _sessions;

        public event EventHandler<ClientSessionDataReceivedEventArgs> SessionDataReceived;
        public event EventHandler<ClientSessionDisconnectedEventArgs> SessionClosed;

        public SessionManager()
        {
            _sessions = new ConcurrentDictionary<string, ClientSession>();
        }
        public ClientSession[] Connections
        {
            get
            {
                return _sessions.Values.ToArray();
            }
        }
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
                (session as IDisposable).Dispose();
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
            // Idempotent: the same session can be removed from several disconnect paths
            // (peer Disconnect packet, socket FIN, ping timeout, UI close). Returning false
            // instead of throwing avoids crashing on the second, harmless remove.
            if (string.IsNullOrEmpty(id))
                return false;

            ClientSession client;
            if (_sessions.TryRemove(id, out client))
            {
                if (client != null)
                {
                    client.OnDataReceived -= OnSessionDataReceivedEvetHandler;
                    client.OnDisconnected -= OnSessionDisconnectedEventHandler;
                    (client as IDisposable).Dispose();
                }
                return true;
            }
            return false;
        }
        public bool Remove(ClientSession session)
        {
            if (session == null)
                return false;

            return Remove(session.SessionId);
        }
        public ClientSession GetByKey(string id)
        {
            // Returns null when the session is absent (callers already null-check the result).
            if (string.IsNullOrEmpty(id))
                return null;

            ClientSession client;
            if (_sessions.TryGetValue(id, out client))
            {
                return client;
            }
            return null;
        }
        public ClientSession New(string id, ClientType type, int width, int height, int bytePerPixel, PixelFormat pixelFormat)
        {
            ClientSession client;
            if (_sessions.TryGetValue(id, out client))
            {
                return client;
            }

            ClientSession session = new ClientSession(id, type, width, height, bytePerPixel, pixelFormat);
            Add(id, session);
            return session;
        }
        public ClientSession AddNewAndListen(string id, ClientType type, int port, int width, int height, int bytePerPixel, PixelFormat pixelFormat)
        {
            ClientSession client;
            if (_sessions.TryGetValue(id, out client))
            {
                return client;
            }

            ClientSession session = new ClientSession(id, type, width, height, bytePerPixel, pixelFormat);
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
        public void ScreenCapture(ClientType type, VScreenType e)
        {
            if (type == ClientType.None) return;

            var sessions = _sessions.Where(x => x.Value.SessionType == type).Select(x => x.Value).ToList();
            foreach (var session in sessions)
            {
                session.ScreenReceived(e);
            }
        }
        #endregion
        #region Events
        private void OnSessionDataReceivedEvetHandler(object sender, ClientSessionDataReceivedEventArgs e)
        {
            if (SessionDataReceived != null)
                SessionDataReceived.Invoke(sender, new ClientSessionDataReceivedEventArgs(e.SessionId, type: e.Type, data: e.Data, isSuccess: e.IsSuccess));
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
