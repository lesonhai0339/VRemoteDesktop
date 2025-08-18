using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using VRemoteDesktop.Events;
using VRemoteDesktop.Services.VTCPClient;

namespace VRemoteDesktop.Services.VTCPClientManager
{
    public class VTCPClientManagerService
    {
        private readonly object _lock = new object();
        private ConcurrentDictionary<string , VClient> _connections;
        public EventHandler<P2PClientDataReceived> TCPClientReceivedEvent;
        public VTCPClientManagerService()
        {
            Connections = new ConcurrentDictionary<string, VClient>();
        }
        #region Properties
        public ConcurrentDictionary<string, VClient> Connections
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
        public void Add(string id, VClient client)
        {
            try
            {
                Connections.TryAdd(id, client);
                client.TCPClientReceived += TCPClientResponseEventHandler;
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
                    client.TCPClientReceived -= TCPClientResponseEventHandler;
                    Connections.TryRemove(id, out _);
                }
            }
            catch (Exception ex)
            {

            }
        }

        public VClient GetByKey(string id)
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
        public VClient New(string id)
        {
            VClient client = new VClient(id);
            Add(id, client);
            return client;
        }
        private void TCPClientResponseEventHandler(object sender, P2PClientDataReceived e)
        {
            Console.WriteLine(e.Type);
            TCPClientReceivedEvent?.Invoke(sender, new P2PClientDataReceived(e.Type, e.Flag, e.Data));
        }

        #endregion
    }
}
