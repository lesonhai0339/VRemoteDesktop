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
        private string _socketId;
        private string _myId;
        private string _myPassword;
        private string _partnerId;
        private string _partnerPassword;


        private Socket _socket;
        private ConcurrentQueue<DataReceive> _tasks;
        private BackgroundWorker _backgroundWorker;
        private BackgroundWorker _backgroundWorker2;

        private CancellationTokenSource _cancellationToken;
        private ConcurrentQueue<object> _screenTasks;
        private ConcurrentQueue<object> _commandTasks;


        //public event EventHandler<EventArgs> TCPClientResponse;
        //public event EventHandler<ConnectEventArgs> Connected;
        //public event EventHandler<LoginEventArgs> LoggedIn;
        //public event EventHandler<P2PRequestConnectEventArgs> P2PrequestConnect;
        //public event EventHandler<P2PAcceptConnectEventArgs> P2PAcceptConnect;
        //public event EventHandler<P2PScreenEventArgs> ScreenReceived;
        //public event EventHandler<P2PScreenEventArgs> RegionsScreenReceived;
        //public event EventHandler<P2PScreenSendResponeEventArgs> SendScreenSucceeded;
        //public event EventHandler<P2PKeyboardEventArgs> KeyboardReceived;
        //public event EventHandler<P2PMouseEventArgs> MouseReceived;
        //public event EventHandler<P2PClipboardEventArgs> ClipboardReceived;
        //public event EventHandler<P2PDisconnectEventArgs> P2PDisconnected;
        //public event EventHandler<P2PChatEventArgs> P2PChatMessageReceived;
        //public event EventHandler<P2PFileSendEventArgs> P2PChatSendFileReceived;

        public event EventHandler<P2PClientDataReceived> TCPClientReceived;
        public TCPClient(string socketId)
        {
            ScreenTasks = new ConcurrentQueue<object>();
            CommandTasks = new ConcurrentQueue<object>();

            _isSocketConnected = false;
            _isP2PConnected = false;
            _isDisposed = false;
            _cancellationToken = new CancellationTokenSource();
            //_timer = new Timer(PingToServer, null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(5));
            Tasks = new ConcurrentQueue<DataReceive>();
            Worker = new BackgroundWorker();
            Worker.WorkerSupportsCancellation = true;

            Worker2 = new BackgroundWorker();
            Worker2.WorkerSupportsCancellation = true;
            if (!Worker2.IsBusy)
            {
                Worker2.RunWorkerAsync();
            }

            _socketId = socketId;
        }
        #region Properties
        public string SocketId
        {
            get => _socketId;
            private set => _socketId = value;
        }
        public string MyId
        {
            get => _myId;
            set => _myId = value;
        }
        public string MyPassword
        {
            get => _myPassword;
            set => _myPassword = value;
        }
        public string PartnerId
        {
            get => _partnerId;
            set => _partnerId = value;
        }
        public string PartnerPassword
        {
            get => _partnerId;
            set => _partnerId = value;
        }
        public ConcurrentQueue<object> ScreenTasks
        {
            get => _screenTasks;
            private set
            {
                _screenTasks = value;
            }
        }
        public ConcurrentQueue<object> CommandTasks
        {
            get => _commandTasks;
            private set
            {
                _commandTasks = value;
            }
        }
        public BackgroundWorker Worker2
        {
            get => _backgroundWorker2;
            set
            {
                if (_backgroundWorker2 != null)
                {
                    _backgroundWorker2.DoWork -= DoWork2;
                }

                _backgroundWorker2 = value;

                if (_backgroundWorker2 != null)
                {
                    _backgroundWorker2.DoWork += DoWork2;
                }
            }
        }
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
                                //LoggedIn?.Invoke(this, new LoginEventArgs(true, task.Data));
                                TCPClientReceived?.Invoke(this, new P2PClientDataReceived(DataType.Login, true, task.Data));
                                break;
                            case DataType.P2PRequestConnect:
                                //P2PrequestConnect?.Invoke(this, new P2PRequestConnectEventArgs(true, task.Data));
                                TCPClientReceived?.Invoke(this, new P2PClientDataReceived(DataType.P2PRequestConnect, true, task.Data));
                                break;
                            case DataType.P2PAcceptConnect:
                                //P2PAcceptConnect?.Invoke(this, new P2PAcceptConnectEventArgs(task.Data));
                                TCPClientReceived?.Invoke(this, new P2PClientDataReceived(DataType.P2PAcceptConnect, true, task.Data));
                                break;
                            case DataType.Disconnect:
                                break;
                            case DataType.Ping:
                                break;
                            case DataType.Pong:
                                Console.WriteLine("Pong received from server");
                                break;
                            case DataType.Screen:
                                //ScreenReceived?.Invoke(this, new P2PScreenEventArgs(ScreenType.FULLSCREEN, task.Data));
                                TCPClientReceived?.Invoke(this, new P2PClientDataReceived(DataType.Screen, true, task.Data));
                                break;
                            case DataType.Chunks:
                                //RegionsScreenReceived?.Invoke(this, new P2PScreenEventArgs(ScreenType.REGIONSCREENS, task.Data));
                                TCPClientReceived?.Invoke(this, new P2PClientDataReceived(DataType.Chunks, true, task.Data));
                                break;
                            case DataType.ScreenOk:
                                //SendScreenSucceeded?.Invoke(this, new P2PScreenSendResponeEventArgs(ScreenType.FULLSCREEN, true));
                                TCPClientReceived?.Invoke(this, new P2PClientDataReceived(DataType.ScreenOk, true, task.Data));

                                break;
                            case DataType.ChunksOk:
                                //SendScreenSucceeded?.Invoke(this, new P2PScreenSendResponeEventArgs(ScreenType.REGIONSCREENS, true));
                                TCPClientReceived?.Invoke(this, new P2PClientDataReceived(DataType.ChunksOk, true, task.Data));

                                break;
                            case DataType.Keyboard:
                                //KeyboardReceived?.Invoke(this,  new P2PKeyboardEventArgs(task.Data));
                                TCPClientReceived?.Invoke(this, new P2PClientDataReceived(DataType.Keyboard, true, task.Data));

                                break;
                            case DataType.Mouse:
                                //MouseReceived?.Invoke(this, new P2PMouseEventArgs(task.Data));
                                TCPClientReceived?.Invoke(this, new P2PClientDataReceived(DataType.Mouse, true, task.Data));

                                break;
                            case DataType.Clipboard:
                                //ClipboardReceived?.Invoke(this, new P2PClipboardEventArgs(task.Data));
                                TCPClientReceived?.Invoke(this, new P2PClientDataReceived(DataType.Clipboard, true, task.Data));

                                break;
                            case DataType.Error:
                                Console.WriteLine("Error: "+ Encoding.ASCII.GetString(task.Data));
                                break;
                            case DataType.LoginFailed:
                                //LoggedIn?.Invoke(this, new LoginEventArgs(false, task.Data));
                                TCPClientReceived?.Invoke(this, new P2PClientDataReceived(DataType.LoginFailed, true, task.Data));

                                break;
                            case DataType.P2PDisconnect:
                                IsP2PConnected = false;
                                //P2PDisconnected?.Invoke(this, new P2PDisconnectEventArgs(true));
                                TCPClientReceived?.Invoke(this, new P2PClientDataReceived(DataType.P2PDisconnect, true, task.Data));

                                break;
                            case DataType.P2PConnectFailed:
                                //P2PrequestConnect?.Invoke(this, new P2PRequestConnectEventArgs(false, task.Data));
                                TCPClientReceived?.Invoke(this, new P2PClientDataReceived(DataType.P2PConnectFailed, true, task.Data));

                                break;
                            case DataType.Message:
                               // P2PChatMessageReceived?.Invoke(this, new P2PChatEventArgs(task.Data));
                                TCPClientReceived?.Invoke(this, new P2PClientDataReceived(DataType.Message, true, task.Data));

                                break;
                            case DataType.RequestSendFile:
                                //P2PChatSendFileReceived?.Invoke(this, new P2PFileSendEventArgs(SendFileType.RequestSendFile, task.Data));
                                TCPClientReceived?.Invoke(this, new P2PClientDataReceived(DataType.RequestSendFile, true, task.Data));

                                break;
                            case DataType.AcceptSendFile:
                                //P2PChatSendFileReceived?.Invoke(this, new P2PFileSendEventArgs(SendFileType.AcceptSendFile, task.Data));
                                TCPClientReceived?.Invoke(this, new P2PClientDataReceived(DataType.AcceptSendFile, true, task.Data));

                                break;
                            case DataType.FileTransfer:
                                //P2PChatSendFileReceived?.Invoke(this, new P2PFileSendEventArgs(SendFileType.FileTransfer, task.Data));
                                TCPClientReceived?.Invoke(this, new P2PClientDataReceived(DataType.FileTransfer, true, task.Data));

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
        private void DoWork2(object sender, DoWorkEventArgs e)
        {
            int count = 0;
            while (!_cancellationToken.IsCancellationRequested)
            {
                

                var taskQueue = DequeueTask();
                if (taskQueue != null)
                {
                    try
                    {
                        if (taskQueue is TaskObject task)
                        {
                            ProcessTask(task);
                        }
                        else if (taskQueue is TaskGroup taskGroup)
                        {
                            foreach (var t in taskGroup.Tasks)
                            {
                                if (CommandTasks.TryPeek(out _))
                                {
                                    break;
                                }
                                ProcessTask(t);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.ForContext("FileName", "RemoteClient").Error(ex, "Dowork error");
                    }
                }
                count++;
                Thread.Sleep(5);
            }
        }
        private void ProcessTask(TaskObject task)
        {
            Send(task.Data);
        }
        private object DequeueTask()
        {
            try
            {
                if (CommandTasks.Count > 0)
                {
                    return CommandTasks.TryDequeue(out var tasks) ? tasks : null;
                }
                else
                {
                    return ScreenTasks.TryDequeue(out var tasks) ? tasks : null;
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "DequeueTask error");
                return null;
            }
        }
        public void AddWork(TaskObject task)
        {
            if (ScreenTasks.Count >= 2)
            {
                // keep last frame and remove all previous frames
                object lastItem = null;
                while (ScreenTasks.TryDequeue(out var item))
                {
                    lastItem = item;
                }
                if (lastItem != null)
                {
                    ScreenTasks.Enqueue(lastItem);
                }
            }
            ScreenTasks.Enqueue(task);
        }
        public void AddWorkGroup(List<TaskObject> tasks)
        {
            ScreenTasks.Enqueue(new TaskGroup(tasks));
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
                if (string.IsNullOrWhiteSpace(ip) || port <= 0)
                {
                    Log.ForContext("FileName", nameof(Connect)).Error("Invalidate argument at Connect method");
                    return;
                }

                IPEndPoint remoteEP;
                if (IPAddress.TryParse(ip, out IPAddress validIp))
                {
                    remoteEP = new IPEndPoint(validIp, port);

                    if (Socket == null || !Socket.Connected)
                    {
                        Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        Socket.NoDelay = true;
                    }
                    Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    Socket.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), Socket);
                }
                else
                {
                    Log.ForContext("FileName", nameof(Connect)).Error("Invalid IP address: {Ip}", ip);
                }
            }
            catch (SocketException ex)
            {
                Log.ForContext("FileName", nameof(Connect)).Error(ex, "Error when connect to relay server");
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", nameof(Connect)).Error(ex, "Unexpected error when connect to relay server");
            }
            finally
            {

            }
        }
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
                    //Connected?.Invoke(this, new ConnectEventArgs(false));
                    TCPClientReceived?.Invoke(this, new P2PClientDataReceived(DataType.Connect, false, new byte[0]));
                    Log.ForContext("FileName", "RemoteClient").Error("Cannot connect to server");
                    return;
                }

                SocketConnected = true;
                if (!Worker.IsBusy)
                {
                    Worker.RunWorkerAsync();
                }
                //Connected?.Invoke(this, new ConnectEventArgs(true));
                TCPClientReceived?.Invoke(this, new P2PClientDataReceived(DataType.Connect, true, new byte[0]));
                StateObject stateObject = new StateObject();
                stateObject.WorkSocket = Socket;
                stateObject.SckId = _socketId;

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
                    Console.WriteLine($"Received {num} bytes from {stateObject.SckId}");
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
