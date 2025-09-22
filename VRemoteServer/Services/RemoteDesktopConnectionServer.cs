using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Threading;
using System.Net;
using System.Collections.Concurrent;
using static VRemoteServer.Utils.Enums;
using Serilog;
using VRemoteServer.Models;
using VRemoteServer.Utils;

namespace VRemoteServer.Services
{
    public class RemoteDesktopConnectionServer
    {
        public class Server
        {
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
            private ConcurrentDictionary<string, ClientInfo2> _connections = new ConcurrentDictionary<string, ClientInfo2>();
            private ConcurrentDictionary<string, ConnectionInfo2> _rooms = new ConcurrentDictionary<string, ConnectionInfo2>();


            public Server(int numberOfConnections = 1000, int receiveBufferSize = 1024 * 8)
            {
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
                Console.WriteLine("Press any key to terminate the server process....");
                Console.ReadKey();
                //try
                //{
                //    while (!_cancel.IsCancellationRequested)
                //    {
                //        // Sleep a bit to avoid busy loop
                //        Thread.Sleep(100);
                //    }
                //}
                //finally
                //{
                //    listenSocket.Close();
                //}
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
                SocketConnection connection = new SocketConnection(e.AcceptSocket,
                      readEventArg,
                      DisconnectCallback,
                      SocketDataCallback);
                readEventArg.UserToken = connection;
                bool willRaiseEvent = e.AcceptSocket.ReceiveAsync(readEventArg);
                if (!willRaiseEvent)
                {
                    ProcessReceive(readEventArg);
                }
            }

            private bool SocketDataCallback(string id, SocketDataType type, SocketConnection connection, byte[] data)
            {
                return type switch
                {
                    SocketDataType.Login => ProcessLogin(id, type, connection, data),
                    SocketDataType.P2PRequestConnect=> ProcessP2PRequestConnect(id, type, connection, data),
                    SocketDataType.P2PAcceptConnect or SocketDataType.P2PRejectConnect=> ProcessRespondP2PRequestConnect(id, type, connection, data),
                    SocketDataType.P2PDataSend => ProcessP2PDataSend(id, type, connection, data),
                    SocketDataType.Screen or SocketDataType.Chunks => ProcessP2PDataSend(id, type, connection, data),
                    SocketDataType.Keyboard 
                    or SocketDataType.Mouse
                    or SocketDataType.ScreenOk
                    or SocketDataType.ChunksOk 
                    or SocketDataType.Clipboard
                    or SocketDataType.Chat => ProcessP2PDataSend(id, type, connection, data),
                    _ => false
                };
            }

            private bool ProcessP2PDataSend(string id, SocketDataType type, SocketConnection connection, byte[] data)
            {
                if (_rooms.TryGetValue(id, out var room))
                {
                    var partner = (connection == room.Sender) ? room.Receiver : room.Sender;
                    Send(partner, id, SocketDataType.P2PDataSend, data);
                }
                return true;
            }

            private void DisconnectCallback(SocketConnection connection)
            {
                //CloseClientSocket(connection.SocketAsyncEventArgs);
            }
            public bool ProcessP2PRequestConnect(string id, SocketDataType type, SocketConnection connection, byte[] data)
            {
                bool flag = false;
                if (_connections.TryGetValue(id, out var partner))
                {
                    string connectionId = Encoding.ASCII.GetString(data, 13, 8);
                    ConnectionInfo2 room = new ConnectionInfo2(connectionId: connectionId, sender: connection);
                    _rooms.TryAdd(connectionId, room);
                    Send(partner.Connection, id, SocketDataType.P2PRequestConnect, data, false);
                }
                else
                {
                    Send(connection, id, SocketDataType.Error, Encoding.ASCII.GetBytes("Login Failed"));
                }
                return flag;
            }
            private bool ProcessLogin(string id, SocketDataType type, SocketConnection connection, byte[] data)
            {
                try
                {
                    try
                    {
                        byte[] rawData = new byte[data.Length - 13];
                        Buffer.BlockCopy(data, 13, rawData, 0, data.Length - 13);

                        IPEndPoint ep = connection.Socket.RemoteEndPoint as IPEndPoint;

                        var clientInfo = Encoding.ASCII.GetString(rawData).Replace(" ", "").Split('|');
                        if (clientInfo.Length != 10)
                        {
                            Send(connection, id, SocketDataType.LoginFailed, new byte[0]);
                            Log.ForContext("FileName", "RemoteDesktopServer")
                                .Error($"Invalid login data from client: {ep.Address}");
                        }

                        var isNullOrEmpty = clientInfo.All(x => x != null);
                        if (!isNullOrEmpty)
                        {
                            Send(connection, id, SocketDataType.LoginFailed, new byte[0]);
                            Log.ForContext("FileName", "RemoteDesktopServer")
                                .Error($"Invalid login data from client: {ep.Address}");
                        }
                        if (clientInfo[0].Length != 8)
                        {
                            Send(connection, id, SocketDataType.LoginFailed, new byte[0]);
                            Log.ForContext("FileName", "RemoteDesktopServer")
                                .Error($"Invalid login data from client: {ep.Address}");
                        }
                        if (clientInfo[1].Length != 4)
                        {
                            Send(connection, id, SocketDataType.LoginFailed, new byte[0]);
                            Log.ForContext("FileName", "RemoteDesktopServer")
                                .Error($"Invalid login data from client: {ep.Address}");
                        }

                        ClientInfo2 loginInfo = new ClientInfo2
                        {
                            Id = clientInfo[0],
                            Password = clientInfo[1],
                            ComputerName = clientInfo[2],
                            Width = int.Parse(clientInfo[3]),
                            Height = int.Parse(clientInfo[4]),
                            MajorVersion = clientInfo[5],
                            MinorVersion = clientInfo[6],
                            Ip = ep.Address.ToString(),
                            PublicIP = ep.Address.ToString(),
                            Port = ep.Port.ToString(),
                            Connection = connection
                        };
                        _connections.TryAdd(loginInfo.Id, loginInfo);

                        byte[] bytesInfo = Encoding.ASCII.GetBytes(loginInfo.PublicIP);
                        Send(connection, id, SocketDataType.Login, bytesInfo);
                    }
                    catch (Exception ex)
                    {
                        Log.ForContext("FileName", "RemoteDesktopServer")
                               .Error(ex, "ProcessLogin error");
                    }
                    ////TODO
                    //SendCommandAsync(connection, id, SocketDataType.Login, new byte[0]);
                    return true;              
                }
                catch (Exception ex)
                {
                    Log.ForContext("FileName", "RemoteDesktopServer")
                           .Error(ex, "ProcessLogin error");
                }
                return false;
            }
            public bool ProcessRespondP2PRequestConnect(string id, SocketDataType type, SocketConnection connection, byte[] data)
            {
                if (type == Enums.SocketDataType.P2PAcceptConnect)
                {
                    if (_rooms.TryGetValue(id, out var room))
                    {
                        room.Receiver = connection;
                        Send(room.Sender, id, SocketDataType.P2PAcceptConnect, data, false);
                    }
                }
                else
                {
                    string connectionId = Encoding.ASCII.GetString(data);
                    try
                    {
                        if (_rooms.TryGetValue(connectionId, out var room))
                        {
                            Send(room.Sender, id, SocketDataType.P2PRejectConnect, new byte[0], true);
                        }
                    }
                    finally
                    {
                        _rooms.TryRemove(connectionId, out _);
                    }
                }
                return true;
            }
            private void Send(SocketConnection connection, string socketId, SocketDataType commandType, byte[] data, bool addHeader = true)
            {
                try
                {
                    if (connection.Socket == null)
                        return;

                    if (!addHeader)
                    {
                        connection.SocketAsyncEventArgs.SetBuffer(data, 0, data.Length);
                    }
                    else
                    {
                        int totalLength = data.Length + 5 + socketId.Length;
                        int offset = 0;

                        // Allocate one big buffer
                        byte[] buffer = new byte[totalLength];

                        // Write totalLength (4 bytes)
                        Array.Copy(BitConverter.GetBytes(totalLength), 0, buffer, offset, 4);
                        offset += 4;

                        // Write commandType (1 byte)
                        buffer[offset] = (byte)commandType;
                        offset += 1;

                        // Write socketId (ASCII string)
                        Array.Copy(Encoding.ASCII.GetBytes(socketId), 0, buffer, offset, socketId.Length);
                        offset += socketId.Length;

                        // Write data
                        Array.Copy(data, 0, buffer, offset, data.Length);
                        offset += data.Length;

                        // Finally set buffer once
                        connection.SocketAsyncEventArgs.SetBuffer(buffer, 0, buffer.Length);
                    }
                    bool willRaiseEvent = connection.Socket.SendAsync(connection.SocketAsyncEventArgs);
                    if (!willRaiseEvent)
                    {
                        ProcessSend(connection.SocketAsyncEventArgs);
                    }
                }
                catch (Exception ex)
                {
                    Log.ForContext("FileName", "RemoteDesktopServer")
                        .Error(ex, "Unexpected error");
                }
            }

            private void ProcessReceive(SocketAsyncEventArgs e)
            {
                if(e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
                {
                    Interlocked.Add(ref totalBytesRead, e.BytesTransferred);
                    Console.WriteLine("The server has read a total of {0} bytes", totalBytesRead);
                    SocketConnection connection = (SocketConnection)e.UserToken;
                    if (e.BytesTransferred > 0)
                        connection.CalCuLateData(e.Offset, e.BytesTransferred);

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
                if(e.SocketError == SocketError.Success)
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
                catch(SocketException socketEx)
                {
                    Console.WriteLine("Socket error: ", socketEx);
                }
                catch(Exception ex)
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
        }
        // Represents a collection of reusable SocketAsyncEventArgs objects.
        class SocketAsyncEventArgsPool
        {
            Stack<SocketAsyncEventArgs> m_pool;

            // Initializes the object pool to the specified size
            //
            // The "capacity" parameter is the maximum number of
            // SocketAsyncEventArgs objects the pool can hold
            public SocketAsyncEventArgsPool(int capacity)
            {
                m_pool = new Stack<SocketAsyncEventArgs>(capacity);
            }

            // Add a SocketAsyncEventArg instance to the pool
            //
            //The "item" parameter is the SocketAsyncEventArgs instance
            // to add to the pool
            public void Push(SocketAsyncEventArgs item)
            {
                if (item == null) { throw new ArgumentNullException("Items added to a SocketAsyncEventArgsPool cannot be null"); }
                lock (m_pool)
                {
                    m_pool.Push(item);
                }
            }

            // Removes a SocketAsyncEventArgs instance from the pool
            // and returns the object removed from the pool
            public SocketAsyncEventArgs Pop()
            {
                lock (m_pool)
                {
                    return m_pool.Pop();
                }
            }

            // The number of SocketAsyncEventArgs instances in the pool
            public int Count
            {
                get { return m_pool.Count; }
            }
        }
        class BufferManager
        {
            int m_numBytes;                 // the total number of bytes controlled by the buffer pool
            byte[] m_buffer;                // the underlying byte array maintained by the Buffer Manager
            Stack<int> m_freeIndexPool;     //
            int m_currentIndex;
            int m_bufferSize;

            public BufferManager(int totalBytes, int bufferSize)
            {
                m_numBytes = totalBytes;
                m_currentIndex = 0;
                m_bufferSize = bufferSize;
                m_freeIndexPool = new Stack<int>();
            }

            // Allocates buffer space used by the buffer pool
            public void InitBuffer()
            {
                // create one big large buffer and divide that
                // out to each SocketAsyncEventArg object
                m_buffer = new byte[m_numBytes];
            }

            // Assigns a buffer from the buffer pool to the
            // specified SocketAsyncEventArgs object
            //
            // <returns>true if the buffer was successfully set, else false</returns>
            public bool SetBuffer(SocketAsyncEventArgs args)
            {

                if (m_freeIndexPool.Count > 0)
                {
                    args.SetBuffer(m_buffer, m_freeIndexPool.Pop(), m_bufferSize);
                }
                else
                {
                    if ((m_numBytes - m_bufferSize) < m_currentIndex)
                    {
                        return false;
                    }
                    args.SetBuffer(m_buffer, m_currentIndex, m_bufferSize);
                    m_currentIndex += m_bufferSize;
                }
                return true;
            }

            // Removes the buffer from a SocketAsyncEventArg object.
            // This frees the buffer back to the buffer pool
            public void FreeBuffer(SocketAsyncEventArgs args)
            {
                m_freeIndexPool.Push(args.Offset);
                args.SetBuffer(null, 0, 0);
            }
        }
        class ClientInfo2
        {
            public ClientInfo2() { }
            public ClientInfo2(string id, string password, string computerName, int width, int height, string majorVersion, string minorVersion, string ip, string publicIP, string port, SocketConnection connection)
            {
                Id = id;
                Password = password;
                ComputerName = computerName;
                Width = width;
                Height = height;
                MajorVersion = majorVersion;
                MinorVersion = minorVersion;
                Ip = ip;
                PublicIP = publicIP;
                Port = port;
                Connection = connection;
            }

            public string Id { get; set; }
            public string Password { get; set; }
            public string ComputerName { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public string MajorVersion { get; set; }
            public string MinorVersion { get; set; }
            public string Ip { get; set; }
            public string Port { get; set; }
            public string PublicIP { get; set; }
            public SocketConnection Connection { get; set; }
            public string ToNetworkString()
            {
                return string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}", Id, Password, ComputerName, Width, Height, MajorVersion, MinorVersion, Ip, Port, PublicIP);
            }
        }
        class ConnectionInfo2
        {
            public ConnectionInfo2(string connectionId, SocketConnection sender)
            {
                ConnectionId = connectionId;
                Sender = sender;
            }
            public ConnectionInfo2(SocketConnection sender, SocketConnection receiver)
            {
                Sender = sender;
                Receiver = receiver;
            }
            public ConnectionInfo2(string connectionId, SocketConnection sender, SocketConnection receiver)
            {
                ConnectionId = connectionId;
                Sender = sender;
                Receiver = receiver;
            }
            public string ConnectionId { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 16);
            public SocketConnection Sender { get; set; }
            public SocketConnection Receiver { get; set; }
        }
        public class SocketConnection : IDisposable
        {
            private string _ip;
            private bool _isDisposed = false;
            public Socket Socket { get; set; }
            public SocketAsyncEventArgs SocketAsyncEventArgs { get; set; }
            public DateTime _lastSendTime { get; set; }
            private readonly TimeSpan _timeout = TimeSpan.FromSeconds(300);
            private readonly CancellationTokenSource _cts = new CancellationTokenSource();

            private Action<SocketConnection> _disconnectCallback;
            private Func<string, SocketDataType, SocketConnection, byte[], bool> _dataCallback;
            private object _lock = new object();


            //data
            private byte[] _currentHeader;
            private byte[] _remainingData;
            private int _dataExpected;
            private int _dataReceived;
            private string _partnerId;

            public SocketConnection(Socket socket, SocketAsyncEventArgs e, Action<SocketConnection> disconnectCallback, Func<string, SocketDataType, SocketConnection, byte[], bool> dataCallback)
            {
                _lastSendTime = DateTime.Now; //init before check timeout
                Socket = socket;
                SocketAsyncEventArgs = e;
                _disconnectCallback = disconnectCallback;
                _dataCallback = dataCallback;
                CheckTimeOut();
            }
            #region Properties
            public string IP
            {
                // if current ip is null, try to get it from RemoteEndPoint
                get => _ip ??= (Socket.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "Unknown";
                private set
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        _ip = "Unknown";
                    }
                    else
                    {
                        _ip = value;
                    }
                }
            }
            #endregion
            #region Methods
            public void ClearHeader()
            {
                _currentHeader = null;
                _remainingData = null;
                _dataExpected = 0;
                _dataReceived = 0;
            }
            private bool CheckAlive()
            {
                try
                {
                    bool part = Socket.Poll(1000, SelectMode.SelectRead);
                    bool part2 = Socket.Available == 0;
                    if (part && part2)
                    {
                        return false; // Socket is disconnected
                    }
                    else
                    {
                        return true; // Socket is connected
                    }
                }
                catch (SocketException)
                {
                    return false; // Socket is disconnected
                }
                catch (ObjectDisposedException)
                {
                    return false; // Socket is disposed
                }
            }
            private void CheckTimeOut()
            {
                Task.Run(async () =>
                {
                    while (!_cts.Token.IsCancellationRequested)
                    {
                        var timer = DateTime.Now - _lastSendTime;
                        if (timer > _timeout)
                        {
                            Log.ForContext("FileName", "Clients").Warning("Client {ClientId} has been idle for too long, disconnecting...", Socket.RemoteEndPoint?.ToString() ?? "Unknown");
                            Dispose();
                            break;
                        }
                        if (!CheckAlive())
                        {
                            Log.ForContext("FileName", "Clients").Warning("Client {ClientId} is not connected anymore, disconnecting...", Socket.RemoteEndPoint?.ToString() ?? "Unknown");
                            Dispose();
                            break;
                        }
                        try
                        {
                            await Task.Delay(10000, _cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                });
            }

            private void ProcessData(string partnerId, SocketDataType command, byte[] buffer)
            {
                if (_dataCallback != null)
                {
                    _dataCallback(partnerId, command, this, buffer);
                }
            }
            public void CalCuLateData(int offset, int dataLength)
            {
                if (SocketAsyncEventArgs.Buffer == null)
                    return;
                if (_remainingData == null)
                    _remainingData = new byte[0];

                byte[] totalData = new byte[_remainingData.Length + dataLength];
                Buffer.BlockCopy(_remainingData, 0, totalData, 0, _remainingData.Length);
                Buffer.BlockCopy(SocketAsyncEventArgs.Buffer, offset, totalData, _remainingData.Length, dataLength);

                int bytesProcessed = 0;

                while (bytesProcessed < totalData.Length)
                {
                    if (_currentHeader == null)
                    {
                        if (totalData.Length - bytesProcessed >= 13)
                        {
                            _currentHeader = new byte[13];
                            Buffer.BlockCopy(totalData, bytesProcessed, _currentHeader, 0, 13);

                            _dataExpected = BitConverter.ToInt32(_currentHeader, 0);
                            _partnerId = Encoding.ASCII.GetString(_currentHeader, 5, 8);
                            _dataReceived = 0;
                        }
                        else
                        {
                            break;
                        }

                    }
                    if (_currentHeader != null)
                    {
                        SocketDataType type = (SocketDataType)_currentHeader[4];
                        //command packet
                        if (_dataExpected == 0)
                        {
                            ProcessData(_partnerId, type, new byte[0]);
                            _dataExpected = 0;
                            _currentHeader = null;
                            bytesProcessed += 13;
                        }
                        else
                        {
                            int remainingDataNeeded = _dataExpected - _dataReceived;
                            int availableData = totalData.Length - bytesProcessed;
                            int dataNeedToReceive = Math.Min(remainingDataNeeded, availableData);

                            if (dataNeedToReceive > 0)
                            {
                                byte[] bytes = new byte[dataNeedToReceive];
                                Buffer.BlockCopy(totalData, bytesProcessed, bytes, 0, dataNeedToReceive);

                                _dataReceived += dataNeedToReceive;
                                bytesProcessed += dataNeedToReceive;


                                ProcessData(_partnerId, type, bytes);
                                if (_dataReceived >= _dataExpected)
                                {
                                    Console.WriteLine($"Complete {_dataExpected} - {_dataReceived} - {(Socket.RemoteEndPoint as IPEndPoint).Address.ToString()}");
                                    Console.WriteLine("-------------------------------\n");
                                    _dataExpected = 0;
                                    _dataReceived = 0;
                                    _currentHeader = null;
                                    _partnerId = null;
                                }
                            }
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                if (bytesProcessed < totalData.Length)
                {
                    int remainingBytes = totalData.Length - bytesProcessed;
                    _remainingData = new byte[remainingBytes];
                    Buffer.BlockCopy(totalData, bytesProcessed, _remainingData, 0, remainingBytes);
                }
                else
                {
                    _remainingData = Array.Empty<byte>();
                }
            }
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
            protected virtual void Dispose(bool disposing)
            {
                if (disposing)
                {
                    if (_isDisposed) return;
                    try
                    {
                        _cts.Cancel();
                        Socket?.Shutdown(SocketShutdown.Both);
                        Socket?.Close();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "An error occurred while disposing the client socket.");
                    }
                    SocketAsyncEventArgs.Dispose();
                    _dataCallback = null;
                    Socket?.Dispose();
                    _disconnectCallback?.Invoke(this);
                    _isDisposed = true;
                }
            }
            #endregion
        }
    }
}
