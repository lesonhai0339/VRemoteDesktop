using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.Networking;

namespace VRemoteServer.RelayServer.Services
{
    public interface IRelayServerManager
    {
        void Dispose();
    }
    public class RelayServerManager : IRelayServerManager, IDisposable
    {
        private bool _disposed; 
        private readonly SocketConnectionManager _connectionManager;
        private readonly RemoteConnectionManager _remoteConnectionManager;
        private readonly Server _server;
        public RelayServerManager(SocketConnectionManager connectionManager, 
            RemoteConnectionManager remoteConnectionManager,
            Server server)
        {
            _disposed = false;
            _connectionManager = connectionManager;
            _remoteConnectionManager = remoteConnectionManager;
            _server = server;
        }

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

            }
            finally
            {
                _disposed = true;
            }
        }
    }
}
