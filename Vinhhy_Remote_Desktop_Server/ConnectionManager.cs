using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Vinhhy_Remote_Desktop_Server
{
    public class ConnectionManager
    {
        private Socket _sck;
        private Queue<Data> _queue;
        private RemotesManager _remotesManager;
        private List<SocketClient> _socketClients;
        public ConnectionManager()
        {
            Remotes = new RemotesManager();
            Queue = new Queue<Data>();  
            Sck = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Sck.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        }
        #region Attributes
        public Socket Sck
        {
            get => _sck;
            private set
            {
                _sck = value;
            }
        }
        public Queue<Data> Queue
        {
            get => _queue;
            private set
            {
                _queue = value;
            }
        }
        public RemotesManager Remotes
        {
            get => _remotesManager;
            private set
            {
                _remotesManager = value;
            }
        }
        public List<SocketClient> SocketClients
        {
            get => _socketClients;
            private set
            {
                _socketClients = value;
            }
        }
        #endregion
        #region Functions
        private async void ProcessQueueHandler()
        {
            while (true)
            {
                Thread.Sleep(10);

                if(Queue.Count > 0)
                {
                    var temp = Queue.Peek();
                    if (!Remotes.IsExisted(temp.Id))
                    {
                        Console.WriteLine($"Queue count {Queue.Count}");
                        continue;
                    }
                    Socket sck = (temp.Type == RemoteType.OWNER)
                                ? Remotes.Get(temp.Id).Owner
                                : Remotes.Get(temp.Id).Partner;
                    if (sck == null)
                    {
                        Console.WriteLine("Remote Socket is Null");
                        continue;
                    }
                    var queue = Queue.Dequeue();
                    int num = await SendQueueData(sck, queue);
                }
            }
        }
        public async Task<int> SendQueueData(Socket sck, Data data)
        {
            int result = 0;
            switch (data.ResponseType)
            {
                case SocketResponseType.SCREEN:
                    result = await sck.SendAsync(new ArraySegment<byte>(data.ByteData), SocketFlags.None);
                    break;
                default:
                    break;

            }
            return result;
        }
        public void DataReceived(Socket sck , byte[] data, int length)
        {
            byte[] byteArray = new byte[length];
            Array.Copy(data, byteArray, length);
            DataSendType type1 = (DataSendType)byteArray[0];
            ConnectType type2 = (ConnectType)byteArray[1];
            byte[] dataReceived = byteArray.Skip(2).ToArray();
            switch (type1)
            {
                case DataSendType.INIT:
                    Console.WriteLine("Init remote control connection!!");
                    if(type2 == ConnectType.OWNER)
                    {
                        Remotes.Add(
                            id: Encoding.ASCII.GetString(dataReceived, 0, 8),
                            owner: sck,
                            partner: null
                        );
                    }
                    else
                    {
                        Remotes.Add(
                            id: Encoding.ASCII.GetString(dataReceived, 0, 8),
                            owner: null,
                            partner: sck
                        );
                    }
                    break;
                case DataSendType.KEYBOARD:
                    Console.WriteLine("Keyboard data received");
                    break;
                case DataSendType.SCREEN:
                    Console.WriteLine("Screen data received");
                    break;
                case DataSendType.CHUNK:
                    Console.WriteLine("Chunk data received");
                    break;
                case DataSendType.FILE:
                    Console.WriteLine("File data received");
                    break;
                case DataSendType.CHAT:
                    Console.WriteLine("Chat data received");
                    break;
                case DataSendType.CONTROL:
                    Console.WriteLine("Control data received");
                    break;
                case DataSendType.DISCONNECT:
                    Console.WriteLine("Socket disconnect");
                    Remotes.Remove(Encoding.ASCII.GetString(dataReceived, 0, 8));
                    SocketClients.Remove(
                        SocketClients.FirstOrDefault(
                            x => x.Socket == sck
                        )
                    );
                    break;
                default:
                    break;
            }
        }
        public async Task Listen(int port = 2399)
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, port);
            Sck.Bind(endPoint);
            Sck.Listen(10);
            try
            {
                while (true)
                {
                    Console.WriteLine("Listening...");
                    Socket clientSocket = await Task.Factory.FromAsync(
                    Sck.BeginAccept,
                    Sck.EndAccept,
                    null);

                    SocketClient client = new SocketClient(clientSocket, DataReceived);
                    SocketClients.Add(client);
                    _ = client.StartReceiving();

                }


            }
            catch (SocketException ex)
            {

            }
            catch (Exception ex)
            {

            }
            finally
            {
                Sck.Close();
            }
        }
        #endregion

    }
}
