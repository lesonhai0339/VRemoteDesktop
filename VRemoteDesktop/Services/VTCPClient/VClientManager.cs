using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.VTCPClient;
using VRemoteServer.Models;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;

namespace VRemoteDesktop.Services.VTCPClient
{
    public class VClientManager : IDisposable
    {
        private readonly object _lock = new object();
        private ConcurrentDictionary<string, VClient> _connections;
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
                lock (_lock)
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
            catch (Exception ex)
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
                if (Connections.TryGetValue(id, out var client))
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
        public VClient New(string id, VClientType type)
        {
            VClient client = new VClient(id, type);
            Add(id, client);
            return client;
        }
        public void AcceptP2PConnect(ClientInfo myInfo, ClientInfo partnerInfo, string connectionId)
        {
            var newClient = New(connectionId, VClientType.Receiver);
            newClient.UpdatePartnerInfo(partnerInfo);
            newClient.RespondToP2PConnectRequest(DataType.P2PAcceptConnect, myInfo.ToNetworkString());
        }
        public void RejectP2PConnect(object sender ,byte[] data)
        {
            if(sender is VClient client)
            {
                string connectionId = ByteArrayHelper.ConvertByteArrayToString(data, 0, 8, EncodingType.ASCII).GetResult();
                client.RespondToP2PConnectRequest(DataType.P2PRejectConnect, connectionId);
            }
        }
        public void ScreenUpdate(ScreenCaptureEventArgs e)
        {
            foreach (var connection in _connections)
            {
                if (connection.Value.ClientType == VClientType.Receiver)
                    connection.Value.SendScreen(e.Type, e.Data, e.TotalSize);
            }
        }
        private void TCPClientResponseEventHandler(object sender, P2PClientDataReceived e)
        {
            ClientDataReceived?.Invoke(sender, e);
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
