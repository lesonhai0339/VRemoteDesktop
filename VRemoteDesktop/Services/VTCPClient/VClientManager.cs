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
using System.Threading;
using System.Windows.Forms;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.VTCPClient;
using VRemoteServer.Models;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;
using static VRemoteDesktop.Utils.Logger;

namespace VRemoteDesktop.Services.VTCPClient
{
    public class VClientManager : IDisposable
    {
        private readonly string DEFAULT_SERVER_IP = AppSettingHelper.Getvalue("RemoteServerIP");
        private readonly string DEFAULT_SERVER_PORT = AppSettingHelper.Getvalue("RemoteServerPort");
        private readonly ConcurrentDictionary<string, VClient> _connections;
        public EventHandler<P2PClientDataReceived> ClientDataReceived;
        public VClientManager()
        {
            _connections = new ConcurrentDictionary<string, VClient>();
        }
        public ConcurrentDictionary<string, VClient> Connections => _connections;
        public bool HasClientOfType(VClientType type)
        {
            foreach(var connection in _connections)
            {
                if(connection.Value.ClientType == type)
                    return true;
            }
            return false;
        }
        public void Add(string id, VClient client)
        {
            if (!_connections.TryAdd(id, client))
            {
                (client as IDisposable)?.Dispose();
                throw new InvalidOperationException(string.Format("Client with Id:{0} already exists", id));
            }
            client.TCPClientReceived += TCPClientResponseEventHandler;
        }
        public bool Remove(string id)
        {
            if (_connections.TryRemove(id, out var client))
            {
                client.TCPClientReceived -= TCPClientResponseEventHandler;
                (client as IDisposable)?.Dispose();
                return true;
            }
            throw new InvalidOperationException(string.Format("Cannot remove connection with Id:{0}", id));
        }

        public VClient GetByKey(string id)
        {
            if (_connections.TryGetValue(id, out var client))
            {
                return client;
            }
            throw new InvalidOperationException(string.Format("Connection with Id:{0} does not exists", id));
        }
        public VClient New(string id, VClientType type)
        {
            if(_connections.TryGetValue(id, out var existed))
            {
                return existed;
            }

            VClient client = new VClient(id, type);
            Add(id, client);
            return client;
        }
        private void TCPClientResponseEventHandler(object sender, P2PClientDataReceived e)
        {
            ClientDataReceived?.Invoke(sender, e);
        }
        public void AcceptP2PConnect(ClientInfo myInfo, ClientInfo partnerInfo, string connectionId)
        {
            try
            {
                var newClient = New(connectionId, VClientType.Receiver);
                newClient.Connect(DEFAULT_SERVER_IP, int.Parse(DEFAULT_SERVER_PORT));
                newClient.UpdatePartnerInfo(partnerInfo);
                newClient.RespondToP2PConnectRequest(DataType.P2PAcceptConnect, myInfo.ToNetworkString());
            }
            catch (Exception ex)
            {
                throw new Exception("AcceptP2PConnect", ex);
            }
        }
        public void RejectP2PConnect(object sender, byte[] data)
        {
            if (sender is VClient client)
            {
                string connectionId = ByteArrayHelper.ConvertByteArrayToString(data, 0, 8, EncodingType.ASCII).GetResult();
                client.RespondToP2PConnectRequest(DataType.P2PRejectConnect, connectionId);
            }
            else
            {
                throw new ArgumentException(string.Format("Invalid arguments"));
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
        public void Send(DataType type, byte[] data)
        {
            foreach (var connection in _connections)
            {
                if (connection.Value.ClientType == VClientType.Receiver)
                    connection.Value.Send(type, data, connection.Value.SocketId, true);
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
    }
}
