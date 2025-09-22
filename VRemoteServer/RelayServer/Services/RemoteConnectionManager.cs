using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.DTOs;

namespace VRemoteServer.RelayServer.Services
{
    public interface IRemoteConnectionManager
    {
        void Dispose();
    }
    public class RemoteConnectionManager: IRemoteConnectionManager, IDisposable 
    {
        private bool _disposed; 
        private ConcurrentDictionary<string, RemoteConnection> _remoteConnection;
        public RemoteConnectionManager()
        {
            _disposed = false;  
            _remoteConnection = new ConcurrentDictionary<string, RemoteConnection>();
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
