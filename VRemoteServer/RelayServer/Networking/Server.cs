using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.Enums;
using VRemoteServer.RelayServer.Events;

namespace VRemoteServer.RelayServer.Networking
{
    public interface IServer
    {
        void Cancel();
        void Init();
        void Start(IPEndPoint endpoint);
        void Send(SocketAsyncEventArgs e, byte[] data);
        event EventHandler<ServerEventArg> ServerEvent;
        void Dispose();
    }
    public class Server: IServer, IDisposable
    {
        private bool _disposed; 
        private int numberOfConnections;
        private int receiveBufferSize;
        BufferManager bufferManager;
        const int opsToPreAlloc = 2;
        Socket listenSocket;
        SocketAsyncEventArgsPool readWritePool;
        int totalBytesRead;
        int numberConnectedSockets;
        Semaphore maxNumberAcceptedClients;
        private CancellationTokenSource _cancel = new CancellationTokenSource();

        public event EventHandler<ServerEventArg> ServerEvent;    
        public Server(int numberOfConnections = 1000, int receiveBufferSize = 1024 * 8)
        {
            _disposed = false;
            this.totalBytesRead = 0;
            this.numberConnectedSockets = 0;
            this.numberOfConnections = numberOfConnections;
            this.receiveBufferSize = receiveBufferSize;
            bufferManager = new BufferManager(this.receiveBufferSize * this.numberOfConnections * opsToPreAlloc,
                            receiveBufferSize);
            readWritePool = new SocketAsyncEventArgsPool(numberOfConnections);
            maxNumberAcceptedClients = new Semaphore(numberOfConnections, numberOfConnections);
        }
        public void Cancel()
        {
            lock (_cancel) { _cancel.Cancel(); }
        }
        public void Init()
        {
            bufferManager.InitBuffer();

            SocketAsyncEventArgs readWriteEventArg;
            for (int i = 0; i < numberOfConnections; i++)
            {
                readWriteEventArg = new SocketAsyncEventArgs();
                readWriteEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(IOCompleted);

                bufferManager.SetBuffer(readWriteEventArg);
                readWritePool.Push(readWriteEventArg);
            }
        }
        public void Start(IPEndPoint endpoint)
        {
            Console.WriteLine($"Start listening on IP: {endpoint.Address} - Port: {endpoint.Port}");
            listenSocket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(endpoint);

            listenSocket.Listen(100);

            SocketAsyncEventArgs acceptEventArg = new SocketAsyncEventArgs();
            acceptEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(AcceptEventArgCompleted);
            StartAccept(acceptEventArg);
            try
            {
                while (!_cancel.IsCancellationRequested)
                {
                    // Sleep a bit to avoid busy loop
                    Thread.Sleep(100);
                }
            }
            finally
            {
                listenSocket.Close();
            }
        }
        public void Send(SocketAsyncEventArgs e, byte[] data)
        {
            //TODO
        }
        private void StartAccept(SocketAsyncEventArgs acceptEventArg)
        {
            bool willRaiseEvent = false;
            while (!willRaiseEvent)
            {
                maxNumberAcceptedClients.WaitOne();

                acceptEventArg.AcceptSocket = null;
                willRaiseEvent = listenSocket.AcceptAsync(acceptEventArg);
                if (!willRaiseEvent)
                {
                    ProcessAccept(acceptEventArg);
                }
            }
        }
        private void IOCompleted(object sender, SocketAsyncEventArgs e)
        {
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    ProcessReceive(e);
                    break;
                case SocketAsyncOperation.Send:
                    ProcessSend(e);
                    break;
                default:
                    Console.WriteLine("The last operation completed on the socket was not receive or send");
                    break;
            }
        }
        private void AcceptEventArgCompleted(object sender, SocketAsyncEventArgs e)
        {
            ProcessAccept(e);

            //Accept the next connection request
            StartAccept(e);
        }
        private void ProcessAccept(SocketAsyncEventArgs e)
        {
            Interlocked.Increment(ref numberConnectedSockets);
            Console.WriteLine("Client connection accepted. There are {0} clients connected to the server", numberConnectedSockets);

            SocketAsyncEventArgs readEventArg = readWritePool.Pop();
            SocketConnection connection = new SocketConnection(e.AcceptSocket,readEventArg);
            readEventArg.UserToken = connection;

            bool willRaiseEvent = e.AcceptSocket.ReceiveAsync(readEventArg);
            if (!willRaiseEvent)
            {
                ProcessReceive(readEventArg);
            }
        }
        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                Interlocked.Add(ref totalBytesRead, e.BytesTransferred);
                Console.WriteLine("The server has read a total of {0} bytes", totalBytesRead);
                SocketConnection connection = (SocketConnection)e.UserToken;

                ServerEvent?.Invoke(connection, new ServerEventArg(ServerEventType.ReceivedData, e.Offset, e.BytesTransferred));

                //if (e.BytesTransferred > 0)
                //    connection.CalCuLateData(e.Offset, e.BytesTransferred);
                //bool willRaiseEvent = connection.Socket.SendAsync(e);
                //if (!willRaiseEvent)
                //{
                //    ProcessSend(e);
                //}
            }
            else
            {
                CloseClientSocket(e);
            }
        }
        private void ProcessSend(SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                try
                {
                    Socket socket = ((SocketConnection)e.UserToken).Socket;
                    bool willRaiseEvent = socket.ReceiveAsync(e);
                    if (!willRaiseEvent)
                    {
                        ProcessReceive(e);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error");
                }
            }
            else
            {
                CloseClientSocket(e);
            }
        }
        private void CloseClientSocket(SocketAsyncEventArgs e)
        {
            Socket socket = ((SocketConnection)e.UserToken).Socket;

            try
            {
                socket.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException socketEx)
            {
                Console.WriteLine("Socket error: ", socketEx);
            }
            catch (Exception ex)
            {
                Console.WriteLine("CloseClientSocket error: ", ex);
            }
            socket.Close();

            // decrement the counter keeping track of the total number of clients connected to the server
            Interlocked.Decrement(ref numberConnectedSockets);

            // Free the SocketAsyncEventArg so they can be reused by another client
            readWritePool.Push(e);


            maxNumberAcceptedClients.Release();
            Console.WriteLine("A client has been disconnected from the server. There are {0} clients connected to the server", numberConnectedSockets);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);  
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing || _disposed) return;
            try
            {
                _cancel.Cancel();
                //BufferManager bufferManager;
                try
                {
                    listenSocket.Shutdown(SocketShutdown.Both);
                }catch { }
                _cancel?.Dispose();
                listenSocket?.Dispose();
                maxNumberAcceptedClients?.Dispose();
                readWritePool = null;
                listenSocket = null;
            }
            finally
            {
                _disposed = true;   
            }
        }
    }

}
