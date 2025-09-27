/*using System;
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
    }
}
*/