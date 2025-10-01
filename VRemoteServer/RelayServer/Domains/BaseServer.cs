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
using System.Collections.Concurrent;
using VRemoteServer.RelayServer.DTOs;

namespace VRemoteServer.RelayServer.Domains
{
    public interface IBaseServer<TDomain, TDomainEvent, TException>
    {
        void SendToDomain(TDomain domain, int offset, int length);
        void SetTime(TDomain domain);
        TDomain CreateDomainFromSocketAsyncEventArgs(SocketAsyncEventArgs read, SocketAsyncEventArgs send, Socket socket, EventHandler<TDomainEvent> dataEvent);
        SocketAsyncEventArgs GetReadSocketAsyncEventArgsFromDomain(TDomain domain);
        SocketAsyncEventArgs GetSendSocketAsyncEventArgsFromDomain(TDomain domain);
        Socket GetSocketFromDomain(TDomain domain);
        SocketConnectionEventType GetEventTypeFromDomainEvent(TDomainEvent domainEvent);
        void UnRegisterEvent(TDomain domain, EventHandler<TDomainEvent> domainEvent);
        TException InitException(Exception ex, string note);
        void Init();
        Task Start(IPEndPoint endpoint);
        void Cancel();
        void Send(TDomain domain, byte[] data);
        void Send(TDomain domain, int offset, int length);
        event EventHandler<TDomainEvent> ServerEvent;
        event EventHandler<TException> ServerErrorEvent;
        void Dispose();
    }
    public abstract class BaseServer<TDomain, TDomainEvent, TException> : IBaseServer<TDomain, TDomainEvent, TException>, IDisposable 
        where TDomain : class, IDisposable, ITrackableDisposable
        where TException : EventArgs
        where TDomainEvent : EventArgs
    {
        private bool _disposed;
        private int numberOfConnections;
        private int receiveBufferSize;
        BufferManager bufferManager;
        const int opsToPreAlloc = 2; //for reader and sender
        Socket listenSocket;
        SocketAsyncEventArgsPool readWritePool;
        SocketAsyncEventArgsPool sendWritePool;
        long totalBytesRead;
        int numberConnectedSockets;
        Semaphore maxNumberAcceptedClients;
        private CancellationTokenSource _cancel = new CancellationTokenSource();
        private ConcurrentDictionary<TDomain, DomainSendState> _sendStates;

        public virtual event EventHandler<TDomainEvent> ServerEvent;
        public virtual event EventHandler<TException> ServerErrorEvent;
        public  BaseServer(int numberOfConnections = 1000, int receiveBufferSize = 1024 * 8)
        {
            _sendStates = new();
            _disposed = false;
            this.totalBytesRead = 0;
            this.numberConnectedSockets = 0;
            this.numberOfConnections = numberOfConnections;
            this.receiveBufferSize = receiveBufferSize;
            bufferManager = new BufferManager(this.receiveBufferSize * this.numberOfConnections * opsToPreAlloc,
                            receiveBufferSize);
            readWritePool = new SocketAsyncEventArgsPool(numberOfConnections);
            sendWritePool = new SocketAsyncEventArgsPool(numberOfConnections);
            maxNumberAcceptedClients = new Semaphore(numberOfConnections, numberOfConnections);
        }
        public abstract void SendToDomain(TDomain domain, int offset, int length);
        public abstract void SetTime(TDomain domain);
        public abstract TDomain CreateDomainFromSocketAsyncEventArgs(SocketAsyncEventArgs read, SocketAsyncEventArgs send, Socket socket, EventHandler<TDomainEvent> dataEvent);
        public abstract (SocketAsyncEventArgs read, SocketAsyncEventArgs send) GetReadAndSendSocketAsyncEventArgsFromDomain(TDomain domain);
        public abstract SocketAsyncEventArgs GetReadSocketAsyncEventArgsFromDomain(TDomain domain);
        public abstract SocketAsyncEventArgs GetSendSocketAsyncEventArgsFromDomain(TDomain domain);
        public abstract Socket GetSocketFromDomain(TDomain domain);
        public abstract SocketConnectionEventType GetEventTypeFromDomainEvent(TDomainEvent domainEvent);
        public abstract void UnRegisterEvent(TDomain domain, EventHandler<TDomainEvent> domainEvent);
        public abstract TException InitException(Exception ex, string note);
        public virtual  void Cancel()
        {
            lock (_cancel) { _cancel.Cancel(); }
        }
        public virtual  void Init()
        {
            bufferManager.InitBuffer();

            SocketAsyncEventArgs readWriteEventArg;
            SocketAsyncEventArgs sendWriteEventArg;
            for (int i = 0; i < numberOfConnections; i++)
            {
                //Receive
                readWriteEventArg = new SocketAsyncEventArgs();
                readWriteEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(IOCompleted);
                bufferManager.SetBuffer(readWriteEventArg);
                readWritePool.Push(readWriteEventArg);

                //Sender
                sendWriteEventArg = new SocketAsyncEventArgs();
                sendWriteEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(IOCompleted);
                bufferManager.SetBuffer(sendWriteEventArg);
                sendWritePool.Push(sendWriteEventArg);
            }
        }
        public virtual async Task Start(IPEndPoint endpoint)
        {
            Log.ForContext("FileName", this.GetType().Name).Information($"Start listening on IP: {endpoint.Address} - Port: {endpoint.Port}");
            listenSocket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            //listenSocket.ReceiveBufferSize = receiveBufferSize;
            //listenSocket.SendBufferSize = receiveBufferSize;
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
        public virtual void Send(TDomain domain, byte[] data)
        {
            try
            {
                var send = GetSendSocketAsyncEventArgsFromDomain(domain);
                Socket socket = GetSocketFromDomain(domain);
                if (socket == null)
                {
                    //CloseClientSocket(domain);
                    return;
                }
                send.SetBuffer(data, 0, data.Length);
                bool willRaiseEvent = socket.SendAsync(send);
                if (!willRaiseEvent)
                {
                    ProcessSend(send);
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
                //ServerErrorEvent?.Invoke(domain, InitException(ex, "Send error"));
            }
        }
        public virtual void Send(TDomain domain, int offset, int length)
        {
            var (read, send) = GetReadAndSendSocketAsyncEventArgsFromDomain(domain);
            Socket socket = GetSocketFromDomain(domain);
            if (socket == null || read.Buffer == null)
            {
                //CloseClientSocket(domain);
                return;
            }
            try
            {
                byte[] data = new byte[length];
                Buffer.BlockCopy(read.Buffer, offset, data, 0, length);

                var sendState = _sendStates.GetOrAdd(domain, _ => new DomainSendState());
                lock (sendState.SendLock)
                {
                    sendState.Queue.Enqueue(data);

                    if (!sendState.IsSending)
                    {
                        sendState.IsSending = true;
                        StartSendQueue(domain, sendState);
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);

                //ServerErrorEvent?.Invoke(domain, InitException(ex, "Send error"));
            }
        }

        private void StartSendQueue(TDomain domain, DomainSendState sendState)
        {
            byte[] dataSend;

            lock (sendState.SendLock)
            {
                if(sendState.Queue.Count == 0)
                {
                    sendState.IsSending = false;
                    return;
                }
                dataSend = sendState.Queue.Dequeue();
            }

            var (read, send) = GetReadAndSendSocketAsyncEventArgsFromDomain(domain);
            Socket socket = GetSocketFromDomain(domain);

            if (socket == null)
            {
                lock (sendState.SendLock)
                {
                    sendState.IsSending = false;
                }
                return;
            }
            try
            {
                send.SetBuffer(dataSend, 0 , dataSend.Length);
                bool willRaiseEvent = socket.SendAsync(send);
                if (!willRaiseEvent)
                {
                    ProcessSend(send);
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
                lock (sendState.SendLock)
                {
                    sendState.IsSending = false;
                }
            }
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
            TDomain domain = (TDomain)e.UserToken;
            SetTime(domain);
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
            Interlocked.Increment(ref numberConnectedSockets);
            //Console.WriteLine("Client connection accepted. There are {0} clients connected to the server", numberConnectedSockets);

            //Do not dispose these, when dispose TDomain must push them back readWritePool and sendWritePool
            SocketAsyncEventArgs readEventArg = readWritePool.Pop();
            SocketAsyncEventArgs sendEventArg = sendWritePool.Pop();
            try
            {
                TDomain domain = CreateDomainFromSocketAsyncEventArgs(readEventArg, sendEventArg, e.AcceptSocket, TDomainEventHandler);
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
            catch(Exception ex)
            {
                Console.WriteLine(ex);

                Console.WriteLine("ProcessAccept error: "+ ex.Message);
                readWritePool.Push(readEventArg); 
                sendWritePool.Push(sendEventArg);
            }
        }
        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            TDomain domain = (TDomain)e.UserToken;
            try
            {
                if(e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
                {
                    Interlocked.Add(ref totalBytesRead, e.BytesTransferred);

                    Socket socket = GetSocketFromDomain(domain);
                    SendToDomain(domain, e.Offset, e.BytesTransferred);

                    e.SetBuffer(e.Offset, receiveBufferSize);
                    bool willRaiseEvent = socket.ReceiveAsync(e);
                    if (!willRaiseEvent)
                    {
                        ProcessReceive(e);
                    }
                }
                else
                {
                    CloseClientSocket(domain);
                }
            }
            catch( Exception ex)
            {
                Console.WriteLine(ex);

                ServerErrorEvent?.Invoke(domain, InitException(ex, "ProcessReceive error"));
            }
        }
        private void ProcessSend(SocketAsyncEventArgs e)
        {
            TDomain domain = (TDomain)e.UserToken;
            try
            {
                if (e.SocketError == SocketError.Success)
                {
                    if (_sendStates.TryGetValue(domain, out var sendState))
                    {
                        StartSendQueue(domain, sendState);
                    }
                }
                else
                {
                    Console.WriteLine("Send error");
                    //CloseClientSocket(domain);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);

                ServerErrorEvent?.Invoke(domain, InitException(ex, "ProcessSend error"));
            }
        }
        private void TDomainEventHandler(object sender, TDomainEvent e)
        {
            if (sender is TDomain domain)
            {
                var type = GetEventTypeFromDomainEvent(e);
                if(type == SocketConnectionEventType.Disconnected)
                {
                    //CloseClientSocket(domain);
                }
                else if (type == SocketConnectionEventType.Data)
                {
                    ServerEvent?.Invoke(domain, e);
                }
                else
                {
                    Log.ForContext("FileName", this.GetType().Name).Information($"Invalid TDomainType {type.GetType()}");
                    CloseClientSocket(domain);
                }
            }
            else
            {
                Log.ForContext("FileName", this.GetType().Name).Information($"Invalid object type {sender.GetType()}");
            }
        }
        private void CloseClientSocket(TDomain domain)
        {
            try
            {
                if (domain.IsDisposed) return;
                UnRegisterEvent(domain, TDomainEventHandler);
                domain.Dispose();
                ServerErrorEvent?.Invoke(domain, InitException(new ObjectDisposedException(nameof(TDomain)), "Object disconnected"));

                var (read, send) = GetReadAndSendSocketAsyncEventArgsFromDomain(domain);
                Socket socket = GetSocketFromDomain(domain);
                var hashCode = socket.GetHashCode();
                Console.WriteLine($"CloseClientSocket on - {hashCode}");
                try
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
                catch (SocketException socketEx)
                {
                    Console.WriteLine(socketEx);

                    ServerErrorEvent?.Invoke(domain, InitException(socketEx, "CloseClientSocket error"));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);

                    ServerErrorEvent?.Invoke(domain, InitException(ex, "CloseClientSocket error"));
                }
                socket.Close();

                // decrement the counter keeping track of the total number of clients connected to the server
                Interlocked.Decrement(ref numberConnectedSockets);

                // Free the SocketAsyncEventArg so they can be reused by another client
                readWritePool.Push(read);
                sendWritePool.Push(send);


                maxNumberAcceptedClients.Release();
                Log.ForContext("FileName", this.GetType().Name).Information("A client has been disconnected from the server. There are {0} clients connected to the server", numberConnectedSockets);
                _sendStates.TryRemove(domain, out _);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);

            }
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
