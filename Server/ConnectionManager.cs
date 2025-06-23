using Server;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Lifetime;
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
                    await client.Socket.SendAsync(ProcessSend(98), SocketFlags.None);
                }
                int dataType = data[0];
                switch (dataType)
                {
                    case 0:
                        //ping
                        await client.Socket.SendAsync(ProcessSend(0), SocketFlags.None);
                        break;
                    case 1:
                        //login
                        bool loginFlag = true;
                        ProcessLogin(client, data, ref loginFlag);
                        if (loginFlag)
                        {
                            await client.Socket.SendAsync(ProcessSend(1), SocketFlags.None);
                        }
                        else
                        {
                            await client.Socket.SendAsync(ProcessSend(97), SocketFlags.None);
                        }
                        break;
                    case 2:
                        //P2P connection
                        bool p2pFlag = await  ProcessP2PConnect(client, data);
                        if (!p2pFlag)
                        {
                            await client.Socket.SendAsync(ProcessSend(99), SocketFlags.None);
                        }
                        break;
                    case 3:
                        //P2P datasend
                        ProcessP2PDataSend(client , data);
                        break;
                    default:
                        break;
                }
            }
            catch
            {
                await client.Socket.SendAsync(ProcessSend(99), SocketFlags.None);
            }
        }
        private ArraySegment<byte> ProcessSend(object data)
        {
            byte[] dataBytes;
            if (data is byte b)
            {
                dataBytes = new byte[] { b };
            }
            else if(data is int c)
            {
                dataBytes = new byte[] { (byte)c };
            }
            else if (data is byte[] arr)
            {
                dataBytes = arr;
            }
            else
            {
                throw new ArgumentException("Data must be of type byte or byte[]");
            }
            byte[] commandAddedPadding = Utils.AddPaddingToBytes(dataBytes);
            return new ArraySegment<byte>(commandAddedPadding);
        }
        private async void ProcessP2PDataSend(Client client, byte[] data)
        {
            try
            {
                var partner = clients.Select(x =>
                {
                    if(x.Value.Client.Client == client)
                    {
                        return x.Value.Remote;
                    }
                    else if(x.Value.Remote.Client == client)
                    {
                        return x.Value.Client;
                    }
                    else
                    {
                        return null;
                    }
                }).FirstOrDefault();
                if (partner != null)
                {
                    byte[] byteData = new byte[data.Length - 1];
                    Array.Copy(data, 1, byteData, 0, data.Length - 1);
                    await partner.Client.Socket.SendAsync(new ArraySegment<byte>(
                        new byte[] {20}
                        .Concat(byteData).ToArray()), 
                        SocketFlags.None);
                }
            }
            catch
            {
                await client.Socket.SendAsync(new ArraySegment<byte>(
                        new byte[] { 99 }),
                        SocketFlags.None);
            }
        }

        private async Task<bool> ProcessP2PConnect(Client client, byte[] data)
        {
            Info me;
            Info remoteClient;
            Console.WriteLine("P2P Connect");
            IPEndPoint ep = client.Socket.RemoteEndPoint as IPEndPoint;
            byte[] byteData = new byte[data.Length - 1];
            Array.Copy(data, 1, byteData, 0, data.Length - 1);
            Console.WriteLine(byteData.Take(10));
            var remote = Encoding.ASCII.GetString(byteData).Replace(" ","").Split('|'); //remove padding 0x20(space)
            if (remote.Length != 3)
            {
                return false;
            }
            lock (_socketStore)
            {
                me = _socketStore.FirstOrDefault(i => i.Id.Equals(remote[0]));
                remoteClient = _socketStore.FirstOrDefault(x => x.Id.Equals(remote[1]) 
                                    && x.Password.Equals(remote[2])
                                    && x.Id != me.Id);
            }
            if (me == null || remoteClient == null)
            {
                return false;
            }
            else
            {
                string sessionId = Guid.NewGuid().ToString("N").Substring(0, 16);
                clients[sessionId] = new Connection
                {
                    SessionId = sessionId,
                    Remote = me,
                    Client = remoteClient
                };
                byte[] meDataSend = new byte[] { 2, 0 }
                    .Concat(Encoding.ASCII.GetBytes($"{sessionId}|"))
                    .Concat(Encoding.ASCII.GetBytes(remoteClient.ToString()))
                    .ToArray();
                await me.Client.Socket.SendAsync(ProcessSend(meDataSend), SocketFlags.None);

                byte[] remoteDataSend = new byte[] { 2, 1 }
                    .Concat(Encoding.ASCII.GetBytes($"{sessionId}|"))
                    .Concat(Encoding.ASCII.GetBytes(me.ToString()))
                    .ToArray();
                await remoteClient.Client.Socket.SendAsync(ProcessSend(remoteDataSend), SocketFlags.None);

            }
            return true;
        }

        private void ProcessLogin(Client client, byte[] data, ref bool flag)
        {
            IPEndPoint ep = client.Socket.RemoteEndPoint as IPEndPoint;
            byte[] byteData = new byte[data.Length - 1];
            Array.Copy(data, 1, byteData, 0, data.Length - 1);
            var login = Encoding.ASCII.GetString(byteData).Replace(" ","").Split('|');
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
            var myKey = clients.FirstOrDefault(x => x.Value.Remote.Client == client || x.Value.Client.Client == client).Key;
            if (myKey != null)
            {
                bool wasRemoved = clients.TryRemove(myKey, out Connection removedConnection);
                if (wasRemoved)
                {
                    Console.WriteLine($"Successfully removed connection {myKey}");
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
