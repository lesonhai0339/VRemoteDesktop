using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.Enums;
using VRemoteServer.RelayServer.Events;
using VRemoteServer.RelayServer.Networking;

namespace VRemoteServer.RelayServer.Domains
{
    public interface IBaseServer<TDomain, TEvent>
    {
        TDomain CreateDomainFromSocketAsyncEventArgs(SocketAsyncEventArgs read, SocketAsyncEventArgs send, Socket socket);
        SocketAsyncEventArgs GetReadSocketAsyncEventArgsFromDomain(TDomain domain);
        SocketAsyncEventArgs GetSendSocketAsyncEventArgsFromDomain(TDomain domain);
        TEvent CreateEventFromData(ServerEventType type, int offset, int length);
        Socket GetSocketFromDomain(TDomain domain);
        void Init();
        Task Start(IPEndPoint endpoint);
        void Cancel();
        void Send(TDomain domain, byte[] data);
        void Send(TDomain domain, int offset, int length);
        void Receive(TDomain domain);
        void Close(TDomain domain);
        event EventHandler<TEvent> ServerEvent;
        void Dispose();
    }
    public abstract class BaseServer<TDomain, TEvent> : IBaseServer<TDomain, TEvent>, IDisposable where TDomain : class where TEvent : EventArgs
    {
        private bool _disposed;
        private int numberOfConnections;
        private int receiveBufferSize;
        BufferManager readBufferManager;
        BufferManager sendBufferManager;
        const int opsToPreAlloc = 2; //for reader and sender
        Socket listenSocket;
        SocketAsyncEventArgsPool readWritePool;
        SocketAsyncEventArgsPool sendWritePool;
        long totalBytesRead;
        int numberConnectedSockets;
        Semaphore maxNumberAcceptedClients;
        private CancellationTokenSource _cancel = new CancellationTokenSource();

        public virtual event EventHandler<TEvent> ServerEvent;
        public  BaseServer(int numberOfConnections = 1000, int receiveBufferSize = 1024 * 8)
        {
            _disposed = false;
            this.totalBytesRead = 0;
            this.numberConnectedSockets = 0;
            this.numberOfConnections = numberOfConnections;
            this.receiveBufferSize = receiveBufferSize;
            readBufferManager = new BufferManager(this.receiveBufferSize * this.numberOfConnections * opsToPreAlloc,
                            receiveBufferSize);
            sendBufferManager = new BufferManager(this.receiveBufferSize * this.numberOfConnections * opsToPreAlloc,
                            receiveBufferSize);
            readWritePool = new SocketAsyncEventArgsPool(numberOfConnections);
            sendWritePool = new SocketAsyncEventArgsPool(numberOfConnections);
            maxNumberAcceptedClients = new Semaphore(numberOfConnections, numberOfConnections);
        }
        public abstract TDomain CreateDomainFromSocketAsyncEventArgs(SocketAsyncEventArgs read, SocketAsyncEventArgs send, Socket socket);
        public abstract (SocketAsyncEventArgs read, SocketAsyncEventArgs send) GetReadAndSendSocketAsyncEventArgsFromDomain(TDomain domain);

        public abstract SocketAsyncEventArgs GetReadSocketAsyncEventArgsFromDomain(TDomain domain);
        public abstract SocketAsyncEventArgs GetSendSocketAsyncEventArgsFromDomain(TDomain domain);
        public abstract TEvent CreateEventFromData(ServerEventType type, int offset, int length);

        public abstract Socket GetSocketFromDomain(TDomain domain);
        public virtual  void Cancel()
        {
            lock (_cancel) { _cancel.Cancel(); }
        }
        public virtual  void Init()
        {
            readBufferManager.InitBuffer();
            sendBufferManager.InitBuffer();

            SocketAsyncEventArgs readWriteEventArg;
            SocketAsyncEventArgs sendWriteEventArg;
            for (int i = 0; i < numberOfConnections; i++)
            {
                //Receive
                readWriteEventArg = new SocketAsyncEventArgs();
                readWriteEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(IOCompleted);
                readBufferManager.SetBuffer(readWriteEventArg);
                readWritePool.Push(readWriteEventArg);

                //Sender
                sendWriteEventArg = new SocketAsyncEventArgs();
                sendWriteEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(IOCompleted);
                sendBufferManager.SetBuffer(sendWriteEventArg);
                sendWritePool.Push(sendWriteEventArg);
            }
        }
        public virtual async Task Start(IPEndPoint endpoint)
        {
            Log.ForContext("FileName", this.GetType().Name).Information($"Start listening on IP: {endpoint.Address} - Port: {endpoint.Port}");
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
                    await Task.Delay(100);
                    //Thread.Sleep(100);
                }
            }
            finally
            {
                listenSocket.Close();
            }
        }
        public virtual  void Send(TDomain domain, byte[] data)
        {
            var send = GetSendSocketAsyncEventArgsFromDomain(domain);
            Socket socket = GetSocketFromDomain(domain);
            if (socket == null)
            {
                CloseClientSocket(domain);
                return;
            }
            send.SetBuffer(data, 0, data.Length);
            bool willRaiseEvent = socket.SendAsync(send);
            if (!willRaiseEvent)
            {
                ProcessSend(send);
            }
        }
        public virtual void Send(TDomain domain, int offset, int length)
        {
            var (read, send) = GetReadAndSendSocketAsyncEventArgsFromDomain(domain);
            Socket socket = GetSocketFromDomain(domain);
            if (socket == null || read.Buffer == null)
            {
                CloseClientSocket(domain);
                return;
            }
            try
            {
                if (send.Buffer == null || send.Buffer.Length < length)
                {
                    send.SetBuffer(new byte[length], 0, length);
                }
                Buffer.BlockCopy(read.Buffer, offset, send.Buffer, 0, length);
                send.SetBuffer(send.Buffer, 0, length);
            }
            catch{}
            bool willRaiseEvent = socket.SendAsync(send);
            if (!willRaiseEvent)
            {
                ProcessSend(send);
            }
        }
        public virtual void Receive(TDomain domain)
        {
            try
            {
                var (read, send) = GetReadAndSendSocketAsyncEventArgsFromDomain(domain);
                Socket socket = GetSocketFromDomain(domain);
                if (socket != null && socket.Connected)
                {
                    bool willRaiseEvent = socket.ReceiveAsync(read);
                    if (!willRaiseEvent)
                    {
                        ProcessReceive(read); // Handle synchronous completion
                    }
                }
                else
                {
                    Close(domain);
                }
            }
            catch (ObjectDisposedException)
            {
                Close(domain);
            }
            catch (SocketException ex)
            {
                Close(domain);
            }
            catch (Exception ex)
            {
                //TODO
                Close(domain);
            }
        }
        public virtual void Close(TDomain domain)
        {
            this.CloseClientSocket(domain);
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
                    Log.ForContext("FileName", this.GetType().Name).Error("The last operation completed on the socket was not receive or send");
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
            try
            {
                Interlocked.Increment(ref numberConnectedSockets);
                Console.WriteLine("Client connection accepted. There are {0} clients connected to the server", numberConnectedSockets);

                SocketAsyncEventArgs readEventArg = readWritePool.Pop();
                SocketAsyncEventArgs sendEventArg = sendWritePool.Pop();
                TDomain domain = CreateDomainFromSocketAsyncEventArgs(readEventArg, sendEventArg, e.AcceptSocket);
                //readEventArg.UserToken = domain;

                Socket socket = GetSocketFromDomain(domain);
                if (socket == null || !socket.Connected)
                {
                    //Remove this
                    CloseClientSocket(domain);
                    return;
                }
                bool willRaiseEvent = socket.ReceiveAsync(readEventArg);
                if (!willRaiseEvent)
                {
                    ProcessReceive(readEventArg);
                }
            }
            catch { }
        }
        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            try
            {
                TDomain domain = (TDomain)e.UserToken;

                if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
                {
                    Interlocked.Add(ref totalBytesRead, e.BytesTransferred);
                    Console.WriteLine("The server has read a total of {0} bytes", totalBytesRead);

                    Socket socket = GetSocketFromDomain(domain);
                    ServerEvent?.Invoke(domain, CreateEventFromData(ServerEventType.ConnectionDataReceived, e.Offset, e.BytesTransferred));
                }
                else
                {
                    CloseClientSocket(domain);
                }
            }
            catch
            {
                
            }
        }
        private void ProcessSend(SocketAsyncEventArgs e)
        {
            try
            {
                TDomain domain = (TDomain)e.UserToken;
                if (e.SocketError == SocketError.Success)
                {
                    try
                    {
                        var (read, send) = GetReadAndSendSocketAsyncEventArgsFromDomain(domain);
                        Socket socket = GetSocketFromDomain(domain);
                        if (socket == null && socket.Connected)
                        {
                            //Remove if error
                            CloseClientSocket(domain);
                        }
                        bool willRaiseEvent = socket.ReceiveAsync(read);
                        if (!willRaiseEvent)
                        {
                            ProcessReceive(read);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.ForContext("FileName", this.GetType().Name).Error(ex, "ProcessSend error");
                    }
                }
                else
                {
                    CloseClientSocket(domain);
                }
            }
            catch
            {
            }
        }
      /*  private void CloseClientSocket(SocketAsyncEventArgs e)
        {
            T domain = (T)e.UserToken;
            Socket socket = (GetSocketFromDomain(domain));
            //ServerEvent?.Invoke(connection, new ServerEventArg(ServerEventType.LostConnection));
            try
            {
                socket.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException socketEx)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(socketEx, "Socket error");
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "CloseClientSocket error");
            }
            socket.Close();

            // decrement the counter keeping track of the total number of clients connected to the server
            Interlocked.Decrement(ref numberConnectedSockets);

            // Free the SocketAsyncEventArg so they can be reused by another client
            readWritePool.Push(e);


            maxNumberAcceptedClients.Release();
            Log.ForContext("FileName", this.GetType().Name).Information("A client has been disconnected from the server. There are {0} clients connected to the server", numberConnectedSockets);
        }*/
        private void CloseClientSocket(TDomain domain)
        {
            var (read, send) = GetReadAndSendSocketAsyncEventArgsFromDomain(domain);
            Socket socket = GetSocketFromDomain(domain);
            //ServerEvent?.Invoke(connection, new ServerEventArg(ServerEventType.LostConnection));
            try
            {
                socket.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException socketEx)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(socketEx, "Socket error");
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "CloseClientSocket error");
            }
            socket.Close();

            // decrement the counter keeping track of the total number of clients connected to the server
            Interlocked.Decrement(ref numberConnectedSockets);

            // Free the SocketAsyncEventArg so they can be reused by another client
            readWritePool.Push(read);
            sendWritePool.Push(send);


            maxNumberAcceptedClients.Release();
            Log.ForContext("FileName", this.GetType().Name).Information("A client has been disconnected from the server. There are {0} clients connected to the server", numberConnectedSockets);
        }
        public virtual  void Dispose()
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
                }
                catch { }
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
