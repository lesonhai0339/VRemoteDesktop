using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRemoteServer.Models;

namespace VRemoteServer.RelayServer.Services
{
    public interface ISocketConnectionManager
    {
        void Dispose();
    }
    public class SocketConnectionManager: ISocketConnectionManager, IDisposable 
    {
        private bool _disposed;
        private ConcurrentDictionary<string, ConnectionInfo> _socketConnections;
        public SocketConnectionManager()
        {
            _disposed = false;
            _socketConnections = new ConcurrentDictionary<string, ConnectionInfo>();
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
