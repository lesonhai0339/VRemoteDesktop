using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using VRemoteDesktop.Models;
using VRemoteDesktop.Enums;
using static VRemoteDesktop.Utils.Logger;

namespace VRemoteDesktop.Services.TCPClient
{
    public class TCPClient : IDisposable
    {
        private bool _isSocketConnected;
        private bool _isP2PConnected;
        private bool _isDisposed;
        private object _lockObject = new object();

        private Socket _socket;
        private ConcurrentQueue<DataReceive> _tasks;
        private BackgroundWorker _backgroundWorker;
        private CancellationTokenSource _cancellationToken;

        public event Action ConnectEvent;
        public event Action<bool> LoginEvent;
        public event Action<bool, byte[]> P2PConnectEvent;
        public event Action<byte[]> ScreenEvent;
        public event Action<byte[]> ChunksEvent;
        public event Action<bool> ScreenSuccessEvent;
        public event Action<bool> ChunksSuccessEvent;
        public event Action<byte[]> KeyboardReceivedEvent;
        public event Action<byte[]> MouseReceivedEvent;
        public event Action<byte[]> ClipboardReceivedEvent;
        public event Action<bool, string> P2PDisconnectedEvent;
        public event Action<byte[]> ChatMessageEvent;
        public event Action<SendFileType, string, byte[]> SendFileEvent;
        public TCPClient()
        {
            _isSocketConnected = false;
            _isP2PConnected = false;
            _isDisposed = false;
            _cancellationToken = new CancellationTokenSource();
            //_timer = new Timer(PingToServer, null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(5));
            Tasks = new ConcurrentQueue<DataReceive>();
            Worker = new BackgroundWorker();
            Worker.WorkerSupportsCancellation = true;
        }
        #region Properties
        public bool IsP2PConnected
        {
            get
            {
                lock (_lockObject)
                {
                    return _isP2PConnected;
                }
            }
            set
            {
                lock (_lockObject)
                {
                    _isP2PConnected = value;
                }
            }
        }
        public Socket Socket
        {
            get => _socket;
            private set
            {
                //if (_socket != null)
                //{
                //    _socket.Close();
                //    _socket.Dispose();
                //}
                _socket = value;
            }
        }
        public bool SocketConnected
        {
            get => _isSocketConnected;
            private set
            {
                _isSocketConnected = value;
            }
        }
        public BackgroundWorker Worker
        {
            get => _backgroundWorker;
            set
            {
                if (_backgroundWorker != null)
                {
                    _backgroundWorker.DoWork -= DoWork;
                }

                _backgroundWorker = value;

                if (_backgroundWorker != null)
                {
                    _backgroundWorker.DoWork += DoWork;
                }
            }
        }
        public ConcurrentQueue<DataReceive> Tasks
        {
            get => _tasks;
            private set
            {
                _tasks = value;
            }
        }
        #endregion
        #region Methods
        private void DoWork(object sender, DoWorkEventArgs e)
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                if (Tasks.TryDequeue(out var task))
                {
                    try
                    {
                        switch (task.Type)
                        {
                            case DataType.Login:
                                LoginEvent?.Invoke(true);
                                break;
                            case DataType.P2PConnect:
                                IsP2PConnected = true;
                                P2PConnectEvent?.Invoke(true, task.Data);
                                break;
                            case DataType.Disconnect:
                                break;
                            case DataType.Ping:
                                break;
                            case DataType.Pong:
                                Console.WriteLine("Pong received from server");
                                break;
                            case DataType.Screen:
                                ScreenEvent?.Invoke(task.Data);
                                break;
                            case DataType.Chunks:
                                ChunksEvent?.Invoke(task.Data);
                                break;
                            case DataType.ScreenOk:
                                ScreenSuccessEvent?.Invoke(true);
                                break;
                            case DataType.ChunksOk:
                                ChunksSuccessEvent?.Invoke(true);
                                break;
                            case DataType.Keyboard:
                                KeyboardReceivedEvent?.Invoke(task.Data);
                                break;
                            case DataType.Mouse:
                                MouseReceivedEvent?.Invoke(task.Data);
                                break;
                            case DataType.Clipboard:
                                ClipboardReceivedEvent?.Invoke(task.Data);
                                break;
                            case DataType.Error:
                                break;
                            case DataType.LoginFailed:
                                LoginEvent?.Invoke(false);
                                break;
                            case DataType.P2PDisconnect:
                                IsP2PConnected = false;
                                P2PDisconnectedEvent?.Invoke(true, task.SessionId);
                                break;
                            case DataType.P2PConnectFailed:
                                P2PConnectEvent?.Invoke(false, task.Data);
                                break;
                            case DataType.Message:
                                ChatMessageEvent?.Invoke(task.Data);
                                break;
                            case DataType.RequestSendFile:
                                SendFileEvent?.Invoke(SendFileType.RequestSendFile, task.SessionId, task.Data);
                                Console.WriteLine("Request to send file received");
                                break;
                            case DataType.AcceptSendFile:
                                SendFileEvent?.Invoke(SendFileType.AcceptSendFile, task.SessionId, task.Data);
                                Console.WriteLine("Request to receive file received");
                                break;
                            case DataType.FileTransfer:
                                SendFileEvent?.Invoke(SendFileType.FileTransfer, task.SessionId, task.Data);
                                Console.WriteLine("File transfer received");
                                break;
                            default:
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.ForContext("FileName", "RemoteClient").Error(ex, "Dowork error");
                    }
                }
                Thread.Sleep(1);
            }
        }
        public void Cancel()
        {
            _cancellationToken.Cancel();
        }
        /// <summary>
        /// Connect to remote server with default IP and port
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        public void Connect(string ip, int port)
        {
            try
            {
                if (!Worker.IsBusy)
                {
                    Worker.RunWorkerAsync();
                }
                IPEndPoint remoteEP;
                if (IPAddress.TryParse(ip, out IPAddress _))
                {
                    remoteEP = new IPEndPoint(IPAddress.Parse(ip), port);

                    if (Socket == null)
                    {
                        Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        Socket.NoDelay = true;
                    }
                    Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    Socket.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), Socket);
                }
                else
                {
                    Log.ForContext("FileName", "RemoteClient").Error("Invalid IP address: {Ip}", ip);
                }
            }
            catch (SocketException ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "Error when connect to relay server");
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "Unexpected error when connect to relay server");
            }
            finally
            {

            }
        }
        /// <summary>
        /// Callback method when the socket is connected to the remote server
        /// </summary>
        /// <param name="ar"></param>
        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                Socket.EndConnect(ar);
                if (Socket.Connected)
                {
                    SocketConnected = true;
                }
                ConnectEvent?.Invoke();
                StateObject stateObject = new StateObject();
                stateObject.WorkSocket = Socket;

                Socket.BeginReceive(stateObject.Buffer, 0, stateObject.BufferSize, SocketFlags.None, new AsyncCallback(DataCallback), stateObject);
            }
            catch (SocketException ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "SocketException when connecting to remote server");
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "Unexpected error when connecting to remote server");
            }
        }
        /// <summary>
        /// callback method when data is received from the remote server
        /// </summary>
        /// <param name="ar"></param>
        private void DataCallback(IAsyncResult ar)
        {
            try
            {
                StateObject stateObject = (StateObject)ar.AsyncState;
                Socket workSocket = stateObject.WorkSocket;
                int num = Socket.EndReceive(ar);
                if (num > 0)
                {
                    stateObject.ByteArrayBuilder.Append(stateObject.Buffer, 0, num);
                    while (!_cancellationToken.Token.IsCancellationRequested)
                    {
                        if (!(stateObject.ByteArrayBuilder.Length >= 20))
                        {
                            break;
                        }
                        int length = BitConverter.ToInt32(stateObject.ByteArrayBuilder.lsByte.GetRange(16, 4).ToArray(), 0);
                        if (!(stateObject.ByteArrayBuilder.Length >= length))
                        {
                            break;
                        }
                        Array src = stateObject.ByteArrayBuilder.Cut(length).ToArray();
                        byte[] data = new byte[length];
                        Buffer.BlockCopy(src, 0, data, 0, data.Length);
                        ProcessReceiveData(data);

                        if (_cancellationToken.IsCancellationRequested)
                            break;
                    }
                }
                try
                {
                    Socket.BeginReceive(stateObject.Buffer, 0, stateObject.Buffer.Length, SocketFlags.None, new AsyncCallback(DataCallback), stateObject);
                }
                catch (SocketException ex)
                {
                    Log.ForContext("FileName", "RemoteClient").Error(ex, "Begin receive error");
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "Unexpected error when receiving data from remote server");
            }
        }
        private void ProcessReceiveData(byte[] bytes)
        {
            try
            {
                byte[] sessionIdBytes = new byte[16];
                Buffer.BlockCopy(bytes, 0, sessionIdBytes, 0, 16);
                string sessionId = Encoding.ASCII.GetString(sessionIdBytes);
                int length = BitConverter.ToInt32(bytes, 16);

                DataType commandType = (DataType)bytes[20];

                byte[] data = new byte[bytes.Length - 20];
                Buffer.BlockCopy(bytes, 20, data, 0, data.Length);

                Tasks.Enqueue(new DataReceive
                {
                    Type = commandType,
                    SessionId = sessionId,
                    Length = length,
                    Data = data
                });
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", GetType().Name).Error(ex, "ProcessReceiveData error");
            }
        }
        public void Send(byte[] data)
        {
            try
            {
                Socket.BeginSend(data, 0, data.Length, SocketFlags.None, (ar) =>
                {
                    try
                    {
                        Socket.EndSend(ar);
                    }
                    catch (SocketException ex)
                    {
                        Log.ForContext("FileName", "RemoteClient").Error(ex, "Send error");
                    }
                    catch (Exception ex)
                    {
                        Log.ForContext("FileName", "RemoteClient").Error(ex, "Send error");
                    }
                }, null);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "Error when sending data to remote server without specific length");
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    if (_cancellationToken != null)
                    {
                        try
                        {
                            _cancellationToken.Cancel();
                            _cancellationToken.Dispose();
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                    }
                    //background worker
                    Worker.CancelAsync();

                    Worker.DoWork -= DoWork;
                    _backgroundWorker.Dispose();

                    //queue
                    if (Tasks != null)
                    {
                        while (Tasks.TryDequeue(out var item))
                        {
                            if (item is IDisposable disposableItem)
                            {
                                disposableItem.Dispose();
                            }
                        }
                    }
                    try
                    {
                        _socket?.Shutdown(SocketShutdown.Both);
                        _socket?.Close();
                        _socket?.Dispose();
                    }
                    catch (Exception)
                    {
                    }
                    // Set flags
                    _isSocketConnected = false;
                    _isP2PConnected = false;
                    _isDisposed = true;
                }
            }
            _isDisposed = true;
        }
        #endregion
    }

}
