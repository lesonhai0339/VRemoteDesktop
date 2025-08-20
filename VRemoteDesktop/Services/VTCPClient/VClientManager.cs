using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.Events;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.VTCPClient;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;

namespace VRemoteDesktop.Services.VTCPClient
{
    public class VClientManager: IDisposable
    {
        private readonly object _lock = new object();
        private ConcurrentDictionary<string , VClient> _connections;
        public EventHandler<P2PClientDataReceived> ClientDataReceived;
        public VClientManager()
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
        public bool Remove(string id)
        {
            try
            {
                if (Connections.TryGetValue(id, out var client))
                {
                    client.TCPClientReceived -= TCPClientResponseEventHandler;
                    return Connections.TryRemove(id, out _);
                }
            }
            catch (Exception ex)
            {
                return false;
            }
            return false;
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

            ClientDataReceived?.Invoke(sender, e);
        }
        public void ScreenUpdate(ScreenCaptureEventArgs e)
        {
            if (_connections.Count < 2) return;
            foreach(var connection in _connections)
            {
                connection.Value.SendScreen(e.Type, e.Data, e.TotalSize);
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _connections.Clear();
            }
        }
        #endregion
    }
}
