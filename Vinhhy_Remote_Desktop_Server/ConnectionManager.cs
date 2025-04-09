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
            set
            {
                _sck = value;
            }
        }
        public Queue<Data> Queue
        {
            get => _queue;
            set
            {
                _queue = value;
            }
        }
        public RemotesManager Remotes
        {
            get => _remotesManager;
            set
            {
                _remotesManager = value;
            }
        }
        #endregion
        #region Functions
        private async void ProcessQueueHandler()
        {
            while (true)
            {
                Thread.Sleep(10);

                var temp = Queue.Peek();
                if (!Remotes.IsExisted(temp.Id))
                {
                    Console.WriteLine($"Queue count {Queue.Count}");
                    continue;
                }
                Socket sck = (temp.Type == RemoteType.OWNER)
                            ?Remotes.Get(temp.Id).Owner 
                            :Remotes.Get(temp.Id).Partner;
                if(sck == null)
                {
                    Console.WriteLine("Remote Socket is Null");
                    continue;
                }
                var queue = Queue.Dequeue();
                int num = await SendQueueData(sck,queue);
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
        public void DataReceived(byte[] data, int length)
        {
            Console.WriteLine("Ok");
        }
        public async Task Listen(string hostName, string port)
        {
            if(IPAddress.TryParse(hostName, out var Ip))
            {
                IPEndPoint endPoint = new IPEndPoint(Ip, int.Parse(port));
                Sck.Bind(endPoint);
                Sck.Listen(10);
                try
                {
                    while (true)
                    {
                        Socket clientSocket = await Task.Factory.FromAsync(
                        Sck.BeginAccept,
                        Sck.EndAccept,
                        null);

                        SocketClient client= new SocketClient(clientSocket, DataReceived);
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
                    Sck.Close();
                }
            }
        }
        #endregion

    }
}
