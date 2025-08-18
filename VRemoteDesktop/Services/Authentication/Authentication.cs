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
using VRemoteDesktop.Services.VTCPClient;

namespace VRemoteDesktop.Services.Authentication
{
    public class Authentication
    {
        public Authentication()
        {
        }
        public void Connect(VClient client, string ip, int port)
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

                    if (client.Socket == null || !client.Socket.Connected)
                    {
                        client.Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        client.Socket.NoDelay = true;
                    }
                    client.Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    client.Socket.BeginConnect(remoteEP, new AsyncCallback(client.ConnectCallback), client.Socket);
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
        internal void Login(VClient client, string data)
        {
            byte[] encoder = Helpers.ByteArrayHelper.ConvertStringToByteArray(data, Enums.EncodingType.ASCII).GetResult();

            client.Send(DataType.Login, encoder);
        }
        internal void P2PConnect(VClient client, string partnerId, string partnetPassword, ClientInfo myInfo)
        {
            string data = Helpers.StringHelper.StringBuilderWithSeparator("|",partnerId, partnetPassword.ToString(), myInfo.ToNetworkString());
            byte[] dataBytes = Helpers.ByteArrayHelper.ConvertStringToByteArray(data, Enums.EncodingType.ASCII).GetResult();
            client.Send(DataType.P2PRequestConnect, dataBytes, partnerId, true);
        }
        public P2PConnectionResponse P2PAuthentication(byte[] bytes, ClientInfo myInfo)
        {
            try
            {
                string data = Helpers.ByteArrayHelper.ConvertByteArrayToString(bytes, 8, bytes.Length - 8, Enums.EncodingType.ASCII).GetResult();
                string[] stringArray = Helpers.StringHelper.StringToStringArrayWithSeparator(data, "|");
                bool flag = myInfo.Id == stringArray[0] && myInfo.Password == stringArray[1];
                if (!flag)
                    return new P2PConnectionResponse(false, null);

                ClientInfo connecter = new ClientInfo
                {
                    Id = stringArray[2],
                    Password = stringArray[3],
                    ComputerName = stringArray[4],
                    Width = int.Parse(stringArray[5]),
                    Height = int.Parse(stringArray[6]),
                    MajorVersion = stringArray[7],
                    MinorVersion = stringArray[8],
                    Ip = stringArray[9],
                    Port = stringArray[10],
                    PublicIP = stringArray[11],
                };

                return new P2PConnectionResponse(true, connecter); ;
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", nameof(P2PAuthentication)).Error(ex, "P2PConnection error");
                return new P2PConnectionResponse(false, null); 
            }
        }
    }
}
