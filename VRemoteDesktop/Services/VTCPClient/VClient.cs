using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
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
        private VClientType _clientType;
        private ClientInfo _partnerInfo;

        private Socket _socket;
        private BackgroundWorker _receiveBackgroundWorker;
        private BackgroundWorker _senderBackgroundWorker;

        private ManualResetEvent _resetEvent;
        private CancellationTokenSource _cts;
        private CancellationToken _cancellationToken;

        private readonly BlockingCollection<DataReceive> _receivetasks;
        private readonly BlockingCollection<object> _sendTasks;

        public event EventHandler<P2PClientDataReceived> TCPClientReceived;
        public event EventHandler<P2PScreenEventArgs> P2PScreenReceived;
        public event EventHandler<P2PChatEventArgs> P2PChatReceived;
        public VClient(string socketId, VClientType clientType)
        {
            _sendTasks = new BlockingCollection<object>();
            _receivetasks = new BlockingCollection<DataReceive>();

            _isSocketConnected = false;
            _isP2PConnected = false;
            _isDisposed = false;
            _cts = new CancellationTokenSource();
            _cancellationToken = _cts.Token;
            //_timer = new Timer(PingToServer, null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(5));
            ReceivedWorker = new BackgroundWorker();
            ReceivedWorker.WorkerSupportsCancellation = true;

            SenderWorker = new BackgroundWorker();
            SenderWorker.WorkerSupportsCancellation = true;
            if (!SenderWorker.IsBusy)
            {
                SenderWorker.RunWorkerAsync();
            }

            _socketId = socketId;
            _clientType = clientType;
            _resetEvent = new ManualResetEvent(false);
            Partner = null;
        }
        #region Properties
        public ClientInfo Partner
        {
            get
            {
                lock (_lockObject)
                {
                    return _partnerInfo;
                }
            }
            private set
            {
                lock (_lockObject)
                {
                    _partnerInfo = value;
                }
            }
        }
        public VClientType ClientType 
        {
            get
            {
                lock (_lockObject)
                {
                    return _clientType;
                }
            }
        }
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
        public BackgroundWorker SenderWorker
        {
            get => _senderBackgroundWorker;
            set
            {
                if (_senderBackgroundWorker != null)
                {
                    _senderBackgroundWorker.DoWork -= SenderDoWork;
                }

                _senderBackgroundWorker = value;

                if (_senderBackgroundWorker != null)
                {
                    _senderBackgroundWorker.DoWork += SenderDoWork;
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
        #endregion
        #region Methods
        private void DataReceivedWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                foreach (var task in _receivetasks.GetConsumingEnumerable(_cancellationToken))
                {
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();
                    try
                    {
                        ThreadPool.GetAvailableThreads(out int workerThreads, out int completionPortThreads);
                        Log.ForContext("FileName", this.GetType().Name + "Threads").Info(": Current_worker_threds: " + workerThreads + " - completion_Port_Threads: " + completionPortThreads + " - current_tasks: "+ _receivetasks.Count);

                      
                        if (task.Type == DataType.Screen || task.Type == DataType.Chunks)
                        {
                            var lastTask = task;

                            //while (Tasks.TryTake(out var t, 0) 
                            //    && t!= null 
                            //    && (t.Type == DataType.Screen || t.Type == DataType.Chunks))
                            //{
                            //    lastTask = t;
                            //}
                            P2PScreenReceived?.Invoke(this, new P2PScreenEventArgs(lastTask.Type, lastTask.Data));
                        }
                        else
                        {
                            switch (task.Type)
                            {
                                case DataType.Message:
                                case DataType.RequestSendFile:
                                case DataType.AcceptSendFile:
                                case DataType.FileTransfer:
                                    P2PChatReceived?.Invoke(this, new P2PChatEventArgs(task.Type, task.Data));
                                    break;
                                default:
                                    _ = Task.Factory.StartNew(() =>
                                    {
                                        try
                                        {
                                            TCPClientReceived?.Invoke(this, new P2PClientDataReceived(task.Type, true, task.Data));
                                        }
                                        catch (Exception ex)
                                        {
                                            Log.ForContext("FileName", this.GetType().Name).Error(ex, "Dowork error");
                                        }
                                    });
                                    
                                    break;
                            }
                        }        
                    }
                    catch (Exception ex)
                    {
                        Log.ForContext("FileName", "RemoteClient").Error(ex, "Dowork error");
                    }
                    stopwatch.Stop();
                    Log.ForContext("FileName", this.GetType().Name + "DataReceivedWork").Error("Elasped time: "+ stopwatch.Elapsed.TotalMilliseconds);
                }
            }
            catch(OperationCanceledException ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "DataReceivedWork error");
            }
        }
        private void SenderDoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                foreach (var obj in _sendTasks.GetConsumingEnumerable(_cancellationToken))
                {
                    try
                    {
                        if(obj is TaskGroup taskGroup)
                        {
                            foreach (var t in taskGroup.Tasks)
                            {
                                ProcessTask(t);
                            }
                        }
                        else if(obj is TaskObject task)
                        {
                            ProcessTask(task);
                        }

                    }
                    catch (Exception ex)
                    {
                        Log.ForContext("FileName", "RemoteClient").Error(ex, "Dowork error");
                    }
                }
            }
            catch (OperationCanceledException ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "DataReceivedWork error");
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
        public void AddWork(TaskObject task)
        {
            _sendTasks.Add(task);
        }
        public void AddWorkGroup(List<TaskObject> tasks, DataType type = DataType.None)
        {
            _sendTasks.Add(new TaskGroup(tasks));
        }
        public void Cancel()
        {
            var stack = Environment.StackTrace;
            Console.WriteLine("Cancel called by:\n" + stack);
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
                _resetEvent.Reset();
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
                    _resetEvent.WaitOne(5000);
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
                _resetEvent.Set();
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
        public void UpdatePartnerInfo(ClientInfo partnerInfo)
        {
            Partner = partnerInfo;
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

                string socketId = BitConverter.ToString(bytes, 5, 8);

                byte[] data = new byte[bytes.Length - 13];
                Buffer.BlockCopy(bytes, 13, data, 0, data.Length);

                _receivetasks.Add(new DataReceive
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
        public byte[] GenerateP2PHeader(DataType type, int dataSize , byte[] socketId)
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
                Send(data);
                //Socket.BeginSend(data, 0, data.Length, SocketFlags.None, (ar) =>
                //{
                //    try
                //    {
                //        Socket.EndSend(ar);
                //    }
                //    catch (SocketException ex)
                //    {
                //        Log.ForContext("FileName", "RemoteClient").Error(ex, "Send error");
                //    }
                //    catch (Exception ex)
                //    {
                //        Log.ForContext("FileName", "RemoteClient").Error(ex, "Send error");
                //    }
                //}, null);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "Error when sending data to remote server without specific length");
            }
        }
        //public void Send(byte[] data)
        //{
        //    try
        //    {
        //        Socket.BeginSend(data, 0, data.Length, SocketFlags.None, (ar) =>
        //        {
        //            try
        //            {
        //                Socket.EndSend(ar);
        //            }
        //            catch (SocketException ex)
        //            {
        //                Log.ForContext("FileName", "RemoteClient").Error(ex, "Send error");
        //            }
        //            catch (Exception ex)
        //            {
        //                Log.ForContext("FileName", "RemoteClient").Error(ex, "Send error");
        //            }
        //        }, null);
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.ForContext("FileName", "RemoteClient").Error(ex, "Error when sending data to remote server without specific length");
        //    }
        //}
        public void Send(byte[] data)
        {
            try
            {
                if(data == null || data.Length == 0)
                {
                    throw new ArgumentException("Missing arguments");
                }
                Sendstate state = new Sendstate
                {
                    Data = data,
                    Remained = data.Length,
                    Sent = 0,
                    Timeout = DateTime.Now
                };
                Send(state);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "Error when sending data to remote server without specific length");
            }
        }
        private void Send(Sendstate state)
        {
            if (!Socket.Connected)
            {
                throw new InvalidOperationException("Socket with id: "+ SocketId + " no available");
            }
            if (DateTime.Now.Subtract(state.Timeout).TotalSeconds > 30)
            {
                throw new TimeoutException("Send timeout");
            }
            Socket.BeginSend(state.Data, state.Sent, state.Remained, SocketFlags.None, SendCallback, state);
        }
        private void SendCallback(IAsyncResult ar)
        {
            var sentState = (Sendstate)ar.AsyncState;
            try
            {
                checked
                {
                    int num = Socket.EndSend(ar);
                    if (num <= 0)
                    {
                        throw new InvalidOperationException("Send error on socket with socket Id: " + SocketId.ToString());
                    }
                    sentState.Sent += num;
                    sentState.Remained -= num;
                    if (sentState.Remained > 0)
                    {
                        Send(sentState);
                    }
                }
            }
            catch(SocketException ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "SendCallback: socket error on socketid: "+ SocketId);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "SendCallback error on socketid: " + SocketId);
            }
        }
        public void Dispose()
        {
            var stack = Environment.StackTrace;
            Console.WriteLine("Dispose called by:\n" + stack);

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
                    if (_receivetasks != null)
                    {
                        foreach(var item in _receivetasks.GetConsumingEnumerable())
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
