using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.Events;
using VRemoteServer.RelayServer.Networking;

namespace VRemoteServer.RelayServer.Services
{
    public interface IRemoteControlManager
    {
        void InitServer();
        Task StartServer(IPEndPoint ep);
        void CancelServer();
        void Dispose();
    }
    public class RemoteControlManager : IRemoteControlManager, IDisposable
    {
        private bool _disposed;
        private readonly IRemoteControlServer _remoteControlServer;
        private readonly IRemoteConnectionManager _remoteConnectionManager;

        public RemoteControlManager(IRemoteControlServer remoteControlServer, IRemoteConnectionManager remoteConnectionManager)
        {
            _disposed = false;
            _remoteControlServer = remoteControlServer;
            _remoteConnectionManager = remoteConnectionManager;

            //Register events
            _remoteControlServer.BaseEvent += RemoteControlEventHandler;

        }
        #region Properties
        #endregion
        #region Methods
        public void InitServer()
        {
            _remoteControlServer.Init();
        }
        public async Task StartServer(IPEndPoint ep)
        {
            if (ep == null)
                throw new ArgumentNullException(nameof(ep));

            await _remoteControlServer.Start(ep);
        }
        public void CancelServer()
        {
            _remoteControlServer.Cancel();
        }
        #endregion
        #region Events
        private void RemoteControlEventHandler(object sender, BaseServerEventArgs e)
        {
            Console.WriteLine("RemoteControl event called");
        }
        #endregion
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        public virtual void Dispose(bool disposing)
        {
            if (!disposing || _disposed) return;
            try
            {
                //TODO: dispose here
                _remoteControlServer?.Dispose();
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}
