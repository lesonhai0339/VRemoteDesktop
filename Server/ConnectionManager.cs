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
        public void Callback(Client client, byte[] data, int length)
        {
            byte[] bytes = new byte[length];
            Array.Copy(data, 0, bytes, 0, length);
            switch ((int)bytes[0])
            {
                case 1:
                    // Init connection
                    int type = bytes[1];
                    string sessionId = Encoding.UTF8.GetString(bytes, 2, length - 2);

                    IPEndPoint endpoint = (IPEndPoint)client.Socket.RemoteEndPoint;
                    string ip = endpoint.Address.ToString();    
                    int port = endpoint.Port;

                    //check existed connection
                    Connection existed = clients.
                        FirstOrDefault(c => c.Key == );
                    if (existed != null)
                    {
                    }
                    if (type == 1)
                    {
                        // assigned for client1
                        Connection connection = new Connection(sessionId: sessionId, client1: client, ip1: ip,port1: port);
                    }
                    else
                    {
                        //assigned for client2
                        Connection connection = new Connection(sessionId: sessionId, client2: client, ip2: ip, port2: port);
                    }
                    break;
                case 2:
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
