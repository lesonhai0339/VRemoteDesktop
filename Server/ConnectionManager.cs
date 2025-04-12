using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class ConnectionManager
    {
        private Socket _sck;
        private ConcurrentDictionary<string,Connection> clients;   
        public ConnectionManager()
        {
            _sck = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _sck.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            clients = new ConcurrentDictionary<string, Connection>();
        }
        public async Task Callback(Client client, byte[] data, int length)
        {
            byte[] bytes = new byte[length];
            Array.Copy(data, 0, bytes, 0, length);
            //byte[1] is type of client
            int type = bytes[1];
            //byte[2] is length of sessionId
            string sessionId = Encoding.UTF8.GetString(bytes, 2, length - 2);

            //byte[0] is type of message
            switch ((int)bytes[0])
            {
                case 1:
                    // Init connection
                    IPEndPoint endpoint = (IPEndPoint)client.Socket.RemoteEndPoint;
                    string ip = endpoint.Address.ToString();    
                    int port = endpoint.Port;

                    //check existed connection
                    if(clients.TryGetValue(sessionId, out Connection connection))
                    {
                        if(type == 1)
                        {
                            // assigned for client1
                            connection.Client1 = client;
                            connection.ClientIP1 = ip;
                            connection.ClientPort1 = port;
                        }
                        else
                        {
                            // assigned for client2
                            connection.Client2 = client;
                            connection.ClientIP2 = ip;
                            connection.ClientPort2 = port;
                        }
                    }
                    else
                    {
                        Connection con;
                        if (type == 1)
                        {
                            // assigned for client1
                            con = new Connection(sessionId: sessionId, client1: client, ip1: ip, port1: port);
                        }
                        else
                        {
                            //assigned for client2
                            con = new Connection(sessionId: sessionId, client2: client, ip2: ip, port2: port);
                        }
                        clients.TryAdd(sessionId, con);
                    }
                    break;
                case 2:
                    //Receive and send P2P data
                    if(clients.TryGetValue(sessionId,out var existedConnection))
                    {
                        if(type == 1)
                        {
                            await existedConnection.Client2.Socket.SendAsync(new ArraySegment<byte>(bytes, 2, length - 2), SocketFlags.None);
                        }
                        else if(type == 2)
                        {
                            await existedConnection.Client1.Socket.SendAsync(new ArraySegment<byte>(bytes, 2, length - 2), SocketFlags.None);
                        }
                        else
                        {
                            await client.Socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("Invalid format type")), SocketFlags.None);
                        }
                    }
                    else
                    {
                        await client.Socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("Session not found")),SocketFlags.None);
                    }
                    break;
                default:
                    break;
            }
        }
        public async Task Listen()
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 2399);
            _sck.Bind(endPoint);
            _sck.Listen(10);
            try
            {
                while (true)
                {
                    Socket cliet = await Task.Factory.FromAsync(
                        _sck.BeginAccept,
                        _sck.EndAccept,
                        null);
                    Client client = new Client(cliet, Callback);
                    _ = client.StartReceiving();
                }
            }
            catch(SocketException ex)
            {

            }
            catch(Exception ex)
            {

            }
            finally
            {
                _sck.Close();
            }
        }
    }
}
