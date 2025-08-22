using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.ConnectionManager;
using VRemoteServer.Models;

namespace VRemoteDesktop.Services.VTCPClient
{
    public interface IP2PConnectionManager
    {

    }
    public class P2PConnectionManager : IDisposable
    {
        private readonly string DEFAULT_SERVER_IP = AppSettingHelper.Getvalue("RemoteServerIP");
        private readonly string DEFAULT_SERVER_PORT = AppSettingHelper.Getvalue("RemoteServerPort");
        private readonly VClientManager _clientManager;
        private readonly IClientInfoManager _clientInfo;
        public P2PConnectionManager(VClientManager clientManager, IClientInfoManager clientInfo) 
        {
            _clientManager = clientManager;
            _clientInfo = clientInfo;
        }
        //public void Authentication(byte[] data)
        //{
        //    if (_clientInfo.IsAuthenticated(data, out ClientInfo partnerInfo, out string connectionId))
        //    {
        //        //P2P request connect succeeeded
        //        AcceptP2PConnect(_clientInfo.GetMyInfo(), partnerInfo, connectionId);

        //        if (_clientManager.HasClientOfType(VClientType.Receiver))
        //            return true;
        //    }
        //    else
        //    {
        //        RejectP2PConnect(sender, e.Data);
        //    }
        //}
        //public void AcceptP2PConnect(ClientInfo myInfo, ClientInfo partnerInfo, string connectionId)
        //{
        //    try
        //    {
        //        var newClient = _clientManager.New(connectionId, VClientType.Receiver);
        //        newClient.Connect(DEFAULT_SERVER_IP, int.Parse(DEFAULT_SERVER_PORT));
        //        newClient.UpdatePartnerInfo(partnerInfo);
        //        newClient.RespondToP2PConnectRequest(DataType.P2PAcceptConnect, myInfo.ToNetworkString());
        //    }
        //    catch (Exception ex)
        //    {
        //        throw new Exception("AcceptP2PConnect", ex);
        //    }
        //}
        //public void RejectP2PConnect(object sender, byte[] data)
        //{
        //    if (sender is VClient client)
        //    {
        //        string connectionId = ByteArrayHelper.ConvertByteArrayToString(data, 0, 8, EncodingType.ASCII).GetResult();
        //        client.RespondToP2PConnectRequest(DataType.P2PRejectConnect, connectionId);
        //    }
        //    else
        //    {
        //        throw new ArgumentException(string.Format("Invalid arguments"));
        //    }
        //}
        //public void ScreenUpdate(DataType type, List<byte[]> array, int size)
        //{
        //    foreach (var connection in _clientManager.Connections)
        //    {
        //        if (connection.Value.ClientType == VClientType.Receiver)
        //            connection.Value.SendScreen(type, array, size);
        //    }
        //}
        //public void Send(DataType type, byte[] data)
        //{
        //    foreach (var connection in _clientManager.Connections)
        //    {
        //        if (connection.Value.ClientType == VClientType.Receiver)
        //            connection.Value.Send(type, data, connection.Value.SocketId, true);
        //    }
        //}
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {

            }
        }
    }
}
 