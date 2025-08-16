using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using VRemoteDesktop.Services.TCPClient;

namespace VRemoteDesktop.Services.VTCPClientManager
{
    public class VTCPClientManagerService
    {
        private readonly object _lock = new object();
        private ConcurrentDictionary<string , TCPClient.TCPClient> _connections;
        public VTCPClientManagerService()
        {
            Connections = new ConcurrentDictionary<string, TCPClient.TCPClient>();
        }
        #region Properties
        public ConcurrentDictionary<string, TCPClient.TCPClient> Connections
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
        public void Add(string id, TCPClient.TCPClient client)
        {
            try
            {
                Connections.TryAdd(id, client);
                client.TCPClientResponse += TCPClientResponseEventHandler;
            }
            catch(Exception ex)
            {

            }
        }
        public void Remove(string id)
        {
            try
            {
                if (Connections.TryGetValue(id, out var client))
                {
                    client.TCPClientResponse -= TCPClientResponseEventHandler;
                    Connections.TryRemove(id, out _);
                }
            }
            catch (Exception ex)
            {

            }
        }

        public TCPClient.TCPClient GetByKey(string id)
        {
            try
            {
                if(Connections.TryGetValue(id, out var client))
                {
                    return client;
                }
                return null;

            }
            catch (Exception ex)
            {
                return null;

            }
        }
        private void TCPClientResponseEventHandler(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
