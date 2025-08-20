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
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.RemoteDesktop;
using VRemoteDesktop.ViewModels;
using VRemoteServer.Models;
using static VRemoteDesktop.Utils.Logger;

namespace VRemoteDesktop.Services.VTCPClient
{
    public class VClient : IDisposable
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
        private BlockingCollection<DataReceive> _tasks;
        private BackgroundWorker _receiveBackgroundWorker;
        private BackgroundWorker _backgroundWorker2;

        private CancellationTokenSource _cts;
        private CancellationToken _cancellationToken;

        private ConcurrentQueue<object> _screenTasks;
        private ConcurrentQueue<object> _commandTasks;

        public event EventHandler<P2PClientDataReceived> TCPClientReceived;
        public event EventHandler<P2PScreenEventArgs> P2PScreenReceived;
        public VClient(string socketId)
        {
            ScreenTasks = new ConcurrentQueue<object>();
            CommandTasks = new ConcurrentQueue<object>();

            _isSocketConnected = false;
            _isP2PConnected = false;
            _isDisposed = false;
            _cts = new CancellationTokenSource();
            _cancellationToken = _cts.Token;
            //_timer = new Timer(PingToServer, null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(5));
            Tasks = new BlockingCollection<DataReceive>();
            ReceivedWorker = new BackgroundWorker();
            ReceivedWorker.WorkerSupportsCancellation = true;

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
        public BackgroundWorker ReceivedWorker
        {
            get => _receiveBackgroundWorker;
            set
            {
                if (_receiveBackgroundWorker != null)
                {
                    _receiveBackgroundWorker.DoWork -= DataReceivedWork;
                }

                _receiveBackgroundWorker = value;

                if (_receiveBackgroundWorker != null)
                {
                    _receiveBackgroundWorker.DoWork += DataReceivedWork;
                }
            }
        }
        public BlockingCollection<DataReceive> Tasks
        {
            get => _tasks;
            private set
            {
                _tasks = value;
            }
        }
        #endregion
        #region Methods
        private void DataReceivedWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                foreach (var task in Tasks.GetConsumingEnumerable(_cancellationToken))
                {
                    try
                    {
                        switch (task.Type)
                        {
                            case DataType.Screen:
                            case DataType.Chunks:
                                ProcessScreenReceived(task);
                                break;
                            case DataType.Connect:
                            case DataType.Login:
                            case DataType.LoginFailed:
                            case DataType.Disconnect:
                            case DataType.Ping:
                            case DataType.Pong:
                            case DataType.Error:
                            case DataType.P2PRequestConnect:
                            case DataType.P2PAcceptConnect:
                            case DataType.P2PDataSend:
                            case DataType.P2PDisconnect:
                            case DataType.P2PConnectFailed:
                            case DataType.ScreenOk:
                            case DataType.ChunksOk:
                            case DataType.Keyboard:
                            case DataType.Mouse:
                            case DataType.Clipboard:
                            case DataType.Message:
                            case DataType.FileTransfer:
                            case DataType.RequestSendFile:
                            case DataType.AcceptSendFile:
                                TCPClientReceived?.Invoke(this, new P2PClientDataReceived(task.Type, true, task.Data));
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
            }
            catch(OperationCanceledException ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "DataReceivedWork error");
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
            if (task.IsSendHeader)
            {
                Send(task.TaskType, task.Data, task.SessionId, true);
            }
            else
            {
                Send(task.Data);
            }
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
                ScreenTasks.Enqueue(task);
            }
            else
            {
                CommandTasks.Enqueue(task);
            }
        }
        public void AddWorkGroup(List<TaskObject> tasks, DataType type = DataType.None)
        {
            if (type == DataType.Screen || type == DataType.Chunks)
            {
                ScreenTasks.Enqueue(new TaskGroup(tasks));
            }
            else
            {
                CommandTasks.Enqueue(new TaskGroup(tasks));
            }
        }

        public void Cancel()
        {
            _cts.Cancel();
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
                if (!ReceivedWorker.IsBusy)
                {
                    ReceivedWorker.RunWorkerAsync();
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
        public void Login(string data)
        {
            byte[] encoder = ByteArrayHelper.ConvertStringToByteArray(data, Enums.EncodingType.ASCII).GetResult();
            Send(DataType.Login, encoder);
        }
        public void P2PHandshake(string partnerId)
        {
            string p2pHandShakeNetworkString = StringHelper.StringBuilderWithSeparator("|", SocketId);
            byte[] p2pHandShakeDataBytes = ByteArrayHelper.ConvertStringToByteArray(p2pHandShakeNetworkString, Enums.EncodingType.ASCII).GetResult();
            Send(DataType.P2PRequestConnect, p2pHandShakeDataBytes, partnerId, true);
        }
        public void P2PInitConnection(string partnerId, string partnerPassword, string myInfo)
        {
            string p2pLoginNetworkString = StringHelper.StringBuilderWithSeparator("|", partnerId, partnerPassword, myInfo);
            byte[] p2pLoginDataBytes = ByteArrayHelper.ConvertStringToByteArray(p2pLoginNetworkString, Enums.EncodingType.ASCII).GetResult();
            Send(DataType.P2PDataSend, p2pLoginDataBytes, SocketId, true);
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
                    while (!_cancellationToken.IsCancellationRequested)
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

                Tasks.Add(new DataReceive
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
        private byte[] GenerateP2PHeader(DataType type, int dataSize , byte[] socketId)
        {
            int totalSize = dataSize + SocketId.Length + 5; // 5 bytes added are 4 for totalSize and 1 for type
            byte[] header = new byte[5 + SocketId.Length];

            Buffer.BlockCopy(BitConverter.GetBytes(totalSize), 0, header, 0, 4);

            header[4] = (byte)type;
            Buffer.BlockCopy(socketId, 0, header, 5, 8);

            return header;
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
        public void SendScreen(DataType type, List<byte[]> data, int totalSize)
        {
            try
            {
                if (data.Count == 0 || totalSize == 0)
                {
                    Log.ForContext("FileName", GetType().Name).Error("Screen missing some value");
                    return;
                }
                byte[] socketId = Encoding.ASCII.GetBytes(SocketId);
                var header = GenerateP2PHeader(type, totalSize, socketId);

                List<TaskObject> tasks = new List<TaskObject>();
                tasks.Add(new TaskObject
                {
                    TaskType = type,
                    Data = header,
                    IsSendHeader = false
                });

                //data
                for (int i = 0; i < data.Count; i++)
                {
                    var task = new TaskObject
                    {
                        TaskType = type,
                        Data = data[i],
                        IsSendHeader = false
                    };

                    tasks.Add(task);
                }
                AddWorkGroup(tasks, DataType.Screen);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", GetType().Name).Error(ex, "ScreenHookEventHandler error");
            }
        }
        private void ProcessScreenReceived(DataReceive e)
        {
            string id1 = Encoding.ASCII.GetString(e.Data, 0, 8);
            byte[] screen = new byte[e.Data.Length - 8];
            Buffer.BlockCopy(e.Data, 8, screen, 0, e.Data.Length - 8);

            ScreenType type = (e.Type == DataType.Screen) ? ScreenType.FULLSCREEN : ScreenType.REGIONSCREENS;
            P2PScreenReceived?.Invoke(this, new P2PScreenEventArgs(type, screen));
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
                    if (_cts != null)
                    {
                        try
                        {
                            _cts.Cancel();
                            _cts.Dispose();
                            _cts = null;
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                    }
                    //background worker
                    ReceivedWorker.CancelAsync();

                    ReceivedWorker.DoWork -= DataReceivedWork;
                    _receiveBackgroundWorker.Dispose();

                    //queue
                    if (Tasks != null)
                    {
                        foreach(var item in Tasks.GetConsumingEnumerable())
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
