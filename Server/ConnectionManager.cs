using Server;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace RemoteServer
{
    public class ConnectionManager
    {
        private Socket _sck;
        private List<Info> _socketStore;
        private ConcurrentDictionary<string,Connection> clients;
        public ConnectionManager()
        {
            _sck = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _sck.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            clients = new ConcurrentDictionary<string, Connection>();
            _socketStore = new List<Info>();
        }
        public async Task Callback(Client client, byte[] data, int length)
        {
            try
            {
                if(data.Length == 0)
                {
                    await client.Socket.SendAsync(new ArraySegment<byte>(new byte[] { 98}), SocketFlags.None);
                }
                int dataType = data[0];
                switch (dataType)
                {
                    case 0:
                        //ping
                        await client.Socket.SendAsync(new ArraySegment<byte>(new byte[] { 1 }), SocketFlags.None);
                        break;
                    case 1:
                        //login
                        bool loginFlag = true;
                        ProcessLogin(client, data, ref loginFlag);
                        if (loginFlag)
                        {
                            await client.Socket.SendAsync(new ArraySegment<byte>(new byte[] { 2 }), SocketFlags.None);
                        }
                        else
                        {
                            await client.Socket.SendAsync(new ArraySegment<byte>(new byte[] { 97 }), SocketFlags.None);
                        }
                        break;
                    case 2:
                        //P2P connection
                        bool p2pFlag = true;
                        ProcessP2PConnect(client, data, ref p2pFlag);
                        if (p2pFlag)
                        {
                            await client.Socket.SendAsync(new ArraySegment<byte>(new byte[] { 10 }), SocketFlags.None);
                        }
                        else
                        {
                            await client.Socket.SendAsync(new ArraySegment<byte>(new byte[] { 90 }), SocketFlags.None);
                        }
                        break;
                    default:
                        break;
                }
            }
            catch
            {
                await client.Socket.SendAsync(new ArraySegment<byte>(new byte[] { 99 }), SocketFlags.None);
            }
        }

        private void ProcessP2PConnect(Client client, byte[] data, ref bool flag)
        {
            Console.WriteLine("P2P Connect");
            IPEndPoint ep = client.Socket.RemoteEndPoint as IPEndPoint;
            byte[] byteData = new byte[data.Length - 1];
            Array.Copy(data, 1, byteData, 0, data.Length - 1);
            var remote = Encoding.ASCII.GetString(byteData).Split('|');
            if (remote.Length != 2)
            {
                flag = false;
                return;
            }
            lock (_socketStore)
            {
                var me = _socketStore.FirstOrDefault(i => i.Ip.Equals(ep.Address.ToString()));
                var remoteClient = _socketStore.FirstOrDefault(x => x.Id.Equals(remote[0]) 
                                    && x.Password.Equals(remote[1])
                                    && x.Id != me.Id);
                if(me == null || remoteClient == null)
                {
                    flag = false;
                    return;
                }
                
            }
        }

        private void ProcessLogin(Client client, byte[] data, ref bool flag)
        {
            IPEndPoint ep = client.Socket.RemoteEndPoint as IPEndPoint;
            byte[] byteData = new byte[data.Length - 1];
            Array.Copy(data, 1, byteData, 0, data.Length - 1);
            var login = Encoding.ASCII.GetString(byteData).Split('|');
            if(login.Length != 7)
            {
                flag = false;
                return;
            }
            Info loginInfo = new Info
            {
                Id = login[0],
                Password = login[1],
                ComputerName = login[2],
                Width = int.Parse(login[3]),
                Height = int.Parse(login[4]),
                MajorVersion = login[5],
                MinorVersion = login[6],
                Ip= ep.Address.ToString(),
                Port = ep.Port.ToString(),
                Client = client
            };
            lock (_socketStore)
            {
                bool isExisted = _socketStore.Exists(x => x.Id.Equals(loginInfo.Id));
                if (!isExisted)
                {
                    _socketStore.Add(loginInfo);
                    flag = true;
                    return;
                }
                else
                {
                    flag = true;
                    return;
                }
            }
        }
        private void RemoveClient(Client client)
        {
            lock (_socketStore)
            {
                var infoToRemove = _socketStore.FirstOrDefault(i => i.Client == client);
                if (infoToRemove != null)
                {
                    Console.WriteLine($"Removing Info for disconnected client: {infoToRemove.Id}");
                    _socketStore.Remove(infoToRemove);
                }
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
                    Console.WriteLine("Listen");
                    Socket sck = await Task.Factory.FromAsync(
                        _sck.BeginAccept,
                        _sck.EndAccept,
                        null);
                    Client client = new Client(sck, Callback, RemoveClient);
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
