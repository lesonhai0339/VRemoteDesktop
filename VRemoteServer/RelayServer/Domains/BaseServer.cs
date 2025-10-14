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
using System.Buffers;
using System.Data;

namespace VRemoteServer.RelayServer.Domains
{
    public interface IBaseServer<TDomain, TDomainEvent, TException>
    {
        void Init();
        Task Start(IPEndPoint endpoint);
        void Cancel();
        void Send(TDomain domain, byte[] data);
        void Send(TDomain domain, int offset, int length);
        bool SendWithRespond(TDomain domain, byte[] data);
        event EventHandler<TDomainEvent> ServerEvent;
        event EventHandler<TException> ServerErrorEvent;
        void Dispose();

        //Abstract methods to implement in inherited class
        void SetTime(TDomain domain);
        void SendToDomain(TDomain domain, int offset, int length);
        void UnRegisterEvent(TDomain domain, EventHandler<TDomainEvent> domainEvent);
        Socket GetSocketFromDomain(TDomain domain);
        TDomain CreateDomainFromSocketAsyncEventArgs(SocketAsyncEventArgs read, SocketAsyncEventArgs send, Socket socket, EventHandler<TDomainEvent> dataEvent);
        SocketAsyncEventArgs GetReadSocketAsyncEventArgsFromDomain(TDomain domain);
        SocketAsyncEventArgs GetSendSocketAsyncEventArgsFromDomain(TDomain domain);
        SocketConnectionEventType GetEventTypeFromDomainEvent(TDomainEvent domainEvent);
        (SocketAsyncEventArgs read, SocketAsyncEventArgs send) GetReadAndSendSocketAsyncEventArgsFromDomain(TDomain domain);
        TException InitException(Exception ex, string note);
    }
    public abstract class BaseServer<TDomain, TDomainEvent, TException> : IBaseServer<TDomain, TDomainEvent, TException>, IDisposable 
        where TDomain : class, IDisposable, ITrackableDisposable
        where TException : EventArgs
        where TDomainEvent : EventArgs
    {
        private int _freePool;
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
        private ConcurrentDictionary<TDomain, QueueSendState> _sendTasks = new ConcurrentDictionary<TDomain, QueueSendState>();


        private readonly IRateLimiter _rateLimit;

        public virtual event EventHandler<TDomainEvent> ServerEvent;
        public virtual event EventHandler<TException> ServerErrorEvent;
        public  BaseServer(IRateLimiter rateLimiter, int numberOfConnections = 1000, int receiveBufferSize = 1024 * 8)
        {
            _freePool = numberOfConnections;
            _disposed = false;
            _rateLimit = rateLimiter;

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

        //Abstract methods to implement in inherited class
        public abstract void SetTime(TDomain domain);
        public abstract void SendToDomain(TDomain domain, int offset, int length);
        public abstract void UnRegisterEvent(TDomain domain, EventHandler<TDomainEvent> domainEvent);
        public abstract Socket GetSocketFromDomain(TDomain domain);
        public abstract TDomain CreateDomainFromSocketAsyncEventArgs(SocketAsyncEventArgs read, SocketAsyncEventArgs send, Socket socket, EventHandler<TDomainEvent> dataEvent);
        public abstract SocketAsyncEventArgs GetReadSocketAsyncEventArgsFromDomain(TDomain domain);
        public abstract SocketAsyncEventArgs GetSendSocketAsyncEventArgsFromDomain(TDomain domain);
        public abstract SocketConnectionEventType GetEventTypeFromDomainEvent(TDomainEvent domainEvent);
        public abstract (SocketAsyncEventArgs read, SocketAsyncEventArgs send) GetReadAndSendSocketAsyncEventArgsFromDomain(TDomain domain);
        public abstract TException InitException(Exception ex, string note);


        #region Methods
        public virtual void Cancel()
        {
            lock (_cancel) { _cancel.Cancel(); }
        }

        public virtual void Init()
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
            //Called when sent or received data to socket, e.UserToken of send is tuple (TDomain, byte[]) but received is TDomain
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
        public virtual void Send(TDomain domain, byte[] data)
        {
            try
            {
                var (read, send) = GetReadAndSendSocketAsyncEventArgsFromDomain(domain);
                Socket socket = GetSocketFromDomain(domain);
                if (socket == null || read.Buffer == null)
                {
                    //CloseClientSocket(domain);
                    return;
                }
                var sendState = _sendTasks.GetOrAdd(domain, _ => new QueueSendState());
                lock (sendState.LockSend)
                {
                    sendState.Queue.Enqueue((data, data.Length, false));
                    if (!sendState.IsSending)
                    {
                        sendState.IsSending = true;
                        BeginSend(domain, sendState);
                    }
                }
            }
            catch { }
        }

        public virtual void Send(TDomain domain, int offset, int length)
        {
            try
            {
                var (read, send) = GetReadAndSendSocketAsyncEventArgsFromDomain(domain);
                Socket socket = GetSocketFromDomain(domain);
                if (socket == null || read.Buffer == null)
                {
                    //CloseClientSocket(domain);
                    return;
                }

                byte[] data = ArrayPool<byte>.Shared.Rent(length);
                Buffer.BlockCopy(read.Buffer, offset, data, 0, length);

                var sendState = _sendTasks.GetOrAdd(domain, _ => new QueueSendState());
                lock (sendState.LockSend)
                {
                    sendState.Queue.Enqueue((data, length, true));
                    if (!sendState.IsSending)
                    {
                        sendState.IsSending = true;
                        BeginSend(domain, sendState);
                    }
                }
            }
            catch { }
        }

        public virtual bool SendWithRespond(TDomain domain, byte[] data)
        {
            try
            {
                var (read, send) = GetReadAndSendSocketAsyncEventArgsFromDomain(domain);
                Socket socket = GetSocketFromDomain(domain);
                if (socket == null || read.Buffer == null)
                {
                    //CloseClientSocket(domain);
                    return false;
                }
                var sendState = _sendTasks.GetOrAdd(domain, _ => new QueueSendState());
                lock (sendState.LockSend)
                {
                    sendState.Queue.Enqueue((data, data.Length, false));
                    if (!sendState.IsSending)
                    {
                        sendState.IsSending = true;
                        BeginSend(domain, sendState);
                    }
                }
                return true;
            }
            catch { return false; }
        }

        private void BeginSend(TDomain domain, QueueSendState sendState)
        {
            try
            {
                byte[] dataSend = null;
                int length;
                bool isPooled;
                lock (sendState.LockSend)
                {
                    if (sendState.Queue.Count == 0)
                    {
                        sendState.IsSending = false;
                        return;
                    }
                    (dataSend, length, isPooled) = sendState.Queue.Dequeue();
                }
                var (read, send) = GetReadAndSendSocketAsyncEventArgsFromDomain(domain);
                Socket socket = GetSocketFromDomain(domain);

                if (socket == null)
                {
                    if (isPooled)
                    {
                        ArrayPool<byte>.Shared.Return(dataSend, clearArray: false);
                    }
                    lock (sendState.LockSend)
                    {
                        sendState.IsSending = false;
                    }
                    return;
                }

                try
                {
                    send.SetBuffer(dataSend, 0, length);

                    send.UserToken = isPooled ? (domain, dataSend) : (domain, null);
                    bool willRaiseEvent = socket.SendAsync(send);
                    if (!willRaiseEvent)
                    {
                        ProcessSend(send);
                    }
                }
                catch
                {
                    if (isPooled)
                    {
                        ArrayPool<byte>.Shared.Return(dataSend, clearArray: false);
                    }
                    lock (sendState.LockSend)
                    {
                        sendState.IsSending = false;
                    }
                }
            }
            catch { }
        }
        private void ProcessSend(SocketAsyncEventArgs e)
        {
            try
            {
                var (domain, dataSend) = ((TDomain, byte[]))e.UserToken;

                e.UserToken = domain;
                //TDomain domain = (TDomain)e.UserToken;
                try
                {
                    if (dataSend != null)
                    {
                        ArrayPool<byte>.Shared.Return(dataSend, clearArray: false);
                    }
                    if (e.SocketError == SocketError.Success)
                    {
                        if (_sendTasks.TryGetValue(domain, out var sendState))
                        {
                            BeginSend(domain, sendState);
                        }
                    }
                    else
                    {
                        if (_sendTasks.TryGetValue(domain, out var sendState))
                        {
                            lock (sendState.LockSend)
                            {
                                sendState.IsSending = false;
                            }
                        }
                    }
                }
                catch
                {
                    if (_sendTasks.TryGetValue(domain, out var sendState))
                    {
                        lock (sendState.LockSend)
                        {
                            sendState.IsSending = false;
                        }
                    }
                }
            }
            catch { }
        }
        private void ProcessAccept(SocketAsyncEventArgs e)
        {
            Socket acceptSocket = e.AcceptSocket;
            string ip = (acceptSocket.RemoteEndPoint as IPEndPoint).Address.ToString();

            if (string.IsNullOrEmpty(ip))
            {
                Log.ForContext("FileName", this.GetType().Name).Information("Cannot get remote ip {0}, reject socket", ip);
                //Reject connection
                CloseSocket(acceptSocket);
                return;
            }

            if (!_rateLimit.CanAccept(ip))
            {
                Log.ForContext("FileName", this.GetType().Name).Information("Cannot accept connection from {0}, reject socket", ip);
                //Reject connection
                CloseSocket(acceptSocket);
                return;
            }

            Interlocked.Increment(ref numberConnectedSockets);
            Log.ForContext("FileName", this.GetType().Name).Information("Client connection accepted. There are {0} clients connected to the server", numberConnectedSockets);

            //Do not dispose these, when dispose TDomain must push them back readWritePool and sendWritePool
            SocketAsyncEventArgs readEventArg = readWritePool.Pop();
            SocketAsyncEventArgs sendEventArg = sendWritePool.Pop();
            try
            {
                TDomain domain = CreateDomainFromSocketAsyncEventArgs(readEventArg, sendEventArg, acceptSocket, TDomainEventHandler);
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
                    //Interlocked.Add(ref totalBytesRead, e.BytesTransferred);

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
                ServerErrorEvent?.Invoke(domain, InitException(ex, "ProcessReceive error"));
            }
        }
        private void TDomainEventHandler(object sender, TDomainEvent e)
        {
            if (sender is TDomain domain)
            {
                var type = GetEventTypeFromDomainEvent(e);
                if (type == SocketConnectionEventType.Disconnected)
                {
                    CloseClientSocket(domain);
                }
                else if (type == SocketConnectionEventType.Data)
                {
                    ServerEvent?.Invoke(domain, e);
                }
                else
                {
                    CloseClientSocket(domain);
                }
            }
        }
        private void CloseSocket(Socket socket)
        {
            try
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
                socket.Dispose();
            }
            catch(SocketException ex)
            {
                //Write log
            }
            catch(Exception ex)
            {
                //Write log
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
                string ip = (socket?.RemoteEndPoint as IPEndPoint)?.Address.ToString(); 

                if (socket != null && socket.Connected)
                {
                    try
                    {
                        socket.Shutdown(SocketShutdown.Both);
                    }
                    catch (SocketException socketEx) { }
                    catch (Exception ex) { }
                    socket.Close();
                    socket?.Dispose();
                }

                // decrement the counter keeping track of the total number of clients connected to the server
                Interlocked.Decrement(ref numberConnectedSockets);

                // Free the SocketAsyncEventArg so they can be reused by another client
                readWritePool.Push(read);
                sendWritePool.Push(send);

                _rateLimit.Release(ip);
                maxNumberAcceptedClients.Release();

                Log.ForContext("FileName", this.GetType().Name).Information("A client has been disconnected from the server. There are {0} clients connected to the server", numberConnectedSockets);

            }
            catch (Exception ex) { }
        }
        #endregion

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
