using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using VRemoteDesktop.Models;
using System.Reflection;
using static VRemoteDesktop.Utils.Logger;
using VRemoteServer.Models;

namespace VRemoteDesktop.Services.Authentication
{
    public class Authentication
    {
        private TCPClient.TCPClient _tcpClient;
        public Authentication(TCPClient.TCPClient tcpClient)
        {
            _tcpClient = tcpClient;
        }
        public void Connect(string ip, int port)
        {
            try
            {
                if(string.IsNullOrWhiteSpace(ip) || port <= 0)
                {
                    Log.ForContext("FileName", nameof(Connect)).Error("Invalidate argument at Connect method");
                    return;
                }

                IPEndPoint remoteEP;
                if (IPAddress.TryParse(ip, out IPAddress validIp))
                {
                    remoteEP = new IPEndPoint(validIp, port);

                    if ( _tcpClient.Socket == null || !_tcpClient.Socket.Connected)
                    {
                        _tcpClient.Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        _tcpClient.Socket.NoDelay = true;
                    }
                    _tcpClient.Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    _tcpClient.Socket.BeginConnect(remoteEP, new AsyncCallback(_tcpClient.ConnectCallback), _tcpClient.Socket);
                }
                else
                {
                    Log.ForContext("FileName", nameof(Connect)).Error("Invalid IP address: {Ip}", ip);
                }
            }
            catch (SocketException ex)
            {
                Log.ForContext("FileName", nameof(Connect)).Error(ex, "Error when connect to relay server");
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", nameof(Connect)).Error(ex, "Unexpected error when connect to relay server");
            }
            finally
            {

            }
        }
        internal void Login(string data)
        {
            byte[] encoder = Helpers.ByteArrayHelper.ConvertStringToByteArray(data, Enums.EncodingType.ASCII).GetResult();

            _tcpClient.Send(DataType.Login, encoder);
        }
        internal void P2PConnect(string partnerId, string partnetPassword, ClientInfo myInfo)
        {
            string data = Helpers.StringHelper.StringBuilderWithSeparator("|",partnerId, partnetPassword.ToString(), myInfo.ToNetworkString());
            byte[] dataBytes = Helpers.ByteArrayHelper.ConvertStringToByteArray(data, Enums.EncodingType.ASCII).GetResult();
            _tcpClient.Send(DataType.P2PConnect, dataBytes, partnerId, true);
        }
        //public bool IsAuthenticated(string id, string password)
        //{
        //    if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(password))
        //        return false;

        //    ClientInfo me = ConnectionManager.ConnectionManager.Me;
        //    return me.Id == id && me.Password == password;
        //}
    }
}
