using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Services.TCPClient;

namespace VRemoteDesktop.Services.VTCPClientManager
{
    public class VTCPClientManagerService
    {
        private readonly object _lock = new object();
        private ConcurrentBag<TCPClient.TCPClient> _connections;
        public VTCPClientManagerService()
        {
            Connections = new ConcurrentBag<TCPClient.TCPClient>();
        }
        #region Properties
        public ConcurrentBag<TCPClient.TCPClient> Connections
        {
            get
            {
                lock (_lock)
                {
                    return _connections;
                }
            }
            private set
            {
                lock(_lock)
                {
                    _connections = value;
                }
            }
        }
        private void AddNewTCPClient(TCPClient.TCPClient client)
        {
            try
            {
                Connections.Add(client);
            }
            catch(Exception ex)
            {

            }
        }
        public void Connect(string id, string password)
        {
            TCPClient.TCPClient client = new TCPClient.TCPClient();
            client.se

        }
        #endregion
    }
}
