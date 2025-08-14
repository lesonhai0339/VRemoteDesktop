using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Models;
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

        public event EventHandler<ConnectEventArgs> ConnectEvent;
        public event EventHandler<LoginEventArgs> LoginEvent;
        public event EventHandler<P2PConnectEventArgs> P2PConnectEvent;
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
            set
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
                                LoginEvent?.Invoke(this, new LoginEventArgs(true, task.Data));
                                break;
                            case DataType.P2PConnect:
                                IsP2PConnected = true;
                                P2PConnectEvent?.Invoke(this, new P2PConnectEventArgs(true, task.Data));
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
                                LoginEvent?.Invoke(this, new LoginEventArgs(false, task.Data));
                                break;
                            case DataType.P2PDisconnect:
                                IsP2PConnected = false;
                                P2PDisconnectedEvent?.Invoke(true, "");
                                break;
                            case DataType.P2PConnectFailed:
                                P2PConnectEvent?.Invoke(this, new P2PConnectEventArgs(false, task.Data));
                                break;
                            case DataType.Message:
                                ChatMessageEvent?.Invoke(task.Data);
                                break;
                            case DataType.RequestSendFile:
                                SendFileEvent?.Invoke(SendFileType.RequestSendFile, "", task.Data);
                                Console.WriteLine("Request to send file received");
                                break;
                            case DataType.AcceptSendFile:
                                SendFileEvent?.Invoke(SendFileType.AcceptSendFile, "", task.Data);
                                Console.WriteLine("Request to receive file received");
                                break;
                            case DataType.FileTransfer:
                                SendFileEvent?.Invoke(SendFileType.FileTransfer, "", task.Data);
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
       
        /// <summary>
        /// Callback method when the socket is connected to the remote server
        /// </summary>
        /// <param name="ar"></param>
        public void ConnectCallback(IAsyncResult ar)
        {
            try
            { 
                Socket.EndConnect(ar);
                if (!Socket.Connected)
                {
                    ConnectEvent?.Invoke(this, new ConnectEventArgs(false));
                    Log.ForContext("FileName", "RemoteClient").Error("Cannot connect to server");
                    return;
                }

                SocketConnected = true;
                if (!Worker.IsBusy)
                {
                    Worker.RunWorkerAsync();
                }
                ConnectEvent?.Invoke(this, new ConnectEventArgs(true));
                StateObject stateObject = new StateObject();
                stateObject.WorkSocket = Socket;

                Socket.BeginReceive(stateObject.Buffer, 0, stateObject.BufferSize, SocketFlags.None, new AsyncCallback(DataCallback), stateObject);
                Log.ForContext("FileName", "RemoteClient").Info("Connected to {RemoteEndPoint}, starting receive loop");
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
                        if (!(stateObject.ByteArrayBuilder.Length >= 5))
                        {
                            break;
                        }
                        int length = BitConverter.ToInt32(stateObject.ByteArrayBuilder.lsByte.GetRange(0, 4).ToArray(), 0);
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
                int length = BitConverter.ToInt32(bytes, 0);

                DataType commandType = (DataType)bytes[4];

                byte[] data = new byte[bytes.Length - 5];
                Buffer.BlockCopy(bytes, 5, data, 0, data.Length);

                Tasks.Enqueue(new DataReceive
                {
                    Type = commandType,
                    Length = length,
                    Data = data
                });
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", GetType().Name).Error(ex, "ProcessReceiveData error");
            }
        }
        private byte[] PrepareHeader(DataType type, string partnerId, byte[] data)
        {
            byte[] resultBytes = new byte[data.Length + 13];

            Buffer.BlockCopy(BitConverter.GetBytes(resultBytes.Length), 0, resultBytes, 0, 4);

            resultBytes[4] = (byte)type;
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(partnerId), 0, resultBytes, 5, 8);
            Buffer.BlockCopy(data, 0, resultBytes, 13, data.Length);

            return resultBytes;
        }
        public void Send(DataType type, byte[] data,string partnerId = "00000000", bool isSendHeader = true)
        {
            try
            {
                if (isSendHeader)
                {
                    data = PrepareHeader(type, partnerId, data);
                }
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
