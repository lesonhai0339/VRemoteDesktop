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
using VRemoteDesktop.Utils;
using VRemoteDesktop.ViewModels;
using VRemoteServer.Models;
using static VRemoteDesktop.Utils.Logger;

namespace VRemoteDesktop.Services.VTCPClient
{
    public class VClient : IDisposable
    {
        private bool _isSocketConnected;
        private bool _isP2PConnected;
        private volatile bool _isDisposed;
        private object _lockObject = new object();
        private string _socketId;
        private VClientType _clientType;
        private ClientInfo _partnerInfo;

        private Socket _socket;
        private BackgroundWorker _receiveBackgroundWorker;
        private BackgroundWorker _senderBackgroundWorker;

        private AutoResetEvent _sckConnect;
        private AutoResetEvent _workAvailable;

        private CancellationTokenSource _cts;
        private CancellationToken _cancellationToken;

        private readonly BlockingCollection<DataReceive> _receivetasks;
        private readonly VPriorityQueue<object, int> _senderTasks;

        public event EventHandler<SocketDisposeEventArgs> SocketDisposing;
        public event EventHandler<P2PClientDataReceived> TCPClientReceived;
        public event EventHandler<P2PScreenEventArgs> P2PScreenReceived;
        public event EventHandler<P2PChatEventArgs> P2PChatReceived;
        public VClient(string socketId, VClientType clientType)
        {
            Partner = null;
            _isDisposed = false;
            _isP2PConnected = false;
            _isSocketConnected = false;
            _socketId = socketId;
            _clientType = clientType;

            _sckConnect = new AutoResetEvent(false);
            _workAvailable = new AutoResetEvent(false);

            _cts = new CancellationTokenSource();
            _cancellationToken = _cts.Token;

            _receivetasks = new BlockingCollection<DataReceive>();
            _senderTasks = new VPriorityQueue<object, int>();

            ReceivedWorker = new BackgroundWorker();
            ReceivedWorker.WorkerSupportsCancellation = true;
            SenderWorker = new BackgroundWorker();
            SenderWorker.WorkerSupportsCancellation = true;
            if (!SenderWorker.IsBusy)
            {
                SenderWorker.RunWorkerAsync();
            }
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
                    try
                    {
                        if (task.Type == SocketDataType.Screen || task.Type == SocketDataType.Chunks)
                        {
                            var lastTask = task;
                            P2PScreenReceived?.Invoke(this, new P2PScreenEventArgs(lastTask.Type, lastTask.Data));
                        }
                        else
                        {
                            switch (task.Type)
                            {
                                case SocketDataType.Chat:
                                    P2PChatReceived?.Invoke(this, new P2PChatEventArgs(task.Type, task.Data));
                                    break;
                                default:                                  
                                    TCPClientReceived?.Invoke(this, new P2PClientDataReceived(task.Type, true, task.Data));
                                    break;
                            }
                        }        
                    }
                    catch (Exception ex)
                    {
                        Log.ForContext("FileName", this.GetType().Name).Error(ex, "Dowork error");
                    }
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
                while (!_cancellationToken.IsCancellationRequested)
                {
                    if (_senderTasks.Dequeue(out var taskObj))
                    {
                        try
                        {
                            if (taskObj is TaskGroup taskGroup)
                            {
                                foreach (var t in taskGroup.Tasks)
                                {
                                    ProcessTask(t);
                                }
                            }
                            else if (taskObj is TaskObject task)
                            {
                                ProcessTask(task);
                            }

                        }
                        catch (Exception ex)
                        {
                            Log.ForContext("FileName", this.GetType().Name).Error(ex, "Dowork error");
                        }
                    }
                    else
                    {
                        _workAvailable.WaitOne(10);
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
            Log.ForContext("FileName", "VClient_QueueHandler").Error(task.TaskType.ToString());
            if (task.TaskType == SocketDataType.Chat)
            {
                ProcessFileTransfer(task);
                return;
            }

            Send(task.TaskType, task.Data, task.SessionId, task.IsSendHeader);
        }
        public void AddWork(TaskObject task)
        {
            _senderTasks.Enqueue(task, (int)task.Priority);
            _workAvailable.Set();
        }
        public void AddWorkGroup(List<TaskObject> tasks, SocketDataType type = SocketDataType.None)
        {
            _senderTasks.Enqueue(new TaskGroup(tasks), (int)tasks[0].Priority);
            _workAvailable.Set();
        }
        public void AddWorkGroup(TaskObject[] tasks, SocketDataType type = SocketDataType.None)
        {
            _senderTasks.Enqueue(new TaskGroup(tasks), (int)tasks[0].Priority);
            _workAvailable.Set();
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
                    _sckConnect.WaitOne(5000);
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
        }
        /// <summary>
        /// Callback method when the socket is connected to the remote server
        /// </summary>
        /// <param name="ar"></param>
        public void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                _sckConnect.Set();
                Socket.EndConnect(ar);
                if (!Socket.Connected)
                {
                    //Connected?.Invoke(this, new ConnectEventArgs(false));
                    TCPClientReceived?.Invoke(this, new P2PClientDataReceived(SocketDataType.Connect, false, new byte[0]));
                    Log.ForContext("FileName", this.GetType().Name).Error("Cannot connect to server");
                    return;
                }

                SocketConnected = true;
                if (!ReceivedWorker.IsBusy)
                {
                    ReceivedWorker.RunWorkerAsync();
                }
                //Connected?.Invoke(this, new ConnectEventArgs(true));
                TCPClientReceived?.Invoke(this, new P2PClientDataReceived(SocketDataType.Connect, true, new byte[0]));
                StateObject stateObject = new StateObject();
                stateObject.WorkSocket = Socket;
                stateObject.SckId = _socketId;

                Socket.BeginReceive(stateObject.Buffer, 0, stateObject.BufferSize, SocketFlags.None, new AsyncCallback(DataCallback), stateObject);
                Log.ForContext("FileName", this.GetType().Name).Info("Connected to {RemoteEndPoint}, starting receive loop");
            }
            catch (SocketException ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "SocketException when connecting to remote server");
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "Unexpected error when connecting to remote server");
            }
        }
        public void UpdatePartnerInfo(ClientInfo partnerInfo)
        {
            Partner = partnerInfo;
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
                    Log.ForContext("FileName", this.GetType().Name).Error(ex, "Begin receive error");
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "Unexpected error when receiving data from remote server");
            }
        }
        private void ProcessReceiveData(byte[] bytes)
        {
            try
            {
                int length = BitConverter.ToInt32(bytes, 0);

                SocketDataType commandType = (SocketDataType)bytes[4];

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
        private byte[] PrepareHeader(SocketDataType type, string partnerId, byte[] data)
        {
            byte[] resultBytes = new byte[data.Length + 13];

            Buffer.BlockCopy(BitConverter.GetBytes(resultBytes.Length), 0, resultBytes, 0, 4);

            resultBytes[4] = (byte)type;
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(partnerId), 0, resultBytes, 5, 8);
            Buffer.BlockCopy(data, 0, resultBytes, 13, data.Length);

            return resultBytes;
        }
        public byte[] GenerateP2PHeader(SocketDataType type, int dataSize , byte[] socketId)
        {
            int totalSize = dataSize + SocketId.Length + 5; // 5 bytes added are 4 for totalSize and 1 for type
            byte[] header = new byte[5 + SocketId.Length];

            Buffer.BlockCopy(BitConverter.GetBytes(totalSize), 0, header, 0, 4);

            header[4] = (byte)type;
            Buffer.BlockCopy(socketId, 0, header, 5, 8);

            return header;
        }
        /// <summary>
        /// Get chunk file data and send to remote server
        /// </summary>
        /// <param name="task"></param>
        /// <exception cref="Exception"></exception>
        private void ProcessFileTransfer(TaskObject task)
        {
            if(task.ChunkFileInfo == null)
            {
                Send(task.TaskType, task.Data, task.SessionId, task.IsSendHeader);
            }
            else
            {
                byte[] chunkFileData = new byte[task.ChunkFileInfo.ChunkSize + 21];
                chunkFileData[0] = task.Data[0]; //first byte is command type
                Buffer.BlockCopy(BitConverter.GetBytes(task.ChunkFileInfo.Offset), 0, chunkFileData, 1, 4);
                Buffer.BlockCopy(Encoding.ASCII.GetBytes(task.ChunkFileInfo.FileId), 0, chunkFileData, 5, 16);
                int chunkRead = FileHelper.GetChunkFileDataByOffset(task.ChunkFileInfo.FilePath, task.ChunkFileInfo.Offset, ref chunkFileData, 21, task.ChunkFileInfo.ChunkSize);

                if (chunkRead != chunkFileData.Length - 21)
                    throw new Exception("ByteRead not the same with bytes data expected");

                Send(task.TaskType, chunkFileData, task.SessionId, task.IsSendHeader);
            }
        }
        /// <summary>
        /// Create packet header before sending to remote server
        /// </summary>
        /// <param name="type"></param>
        /// <param name="data"></param>
        /// <param name="partnerId"></param>
        /// <param name="isSendHeader"></param>
        public void Send(SocketDataType type, byte[] data,string partnerId, bool isSendHeader = true)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(partnerId))
                    partnerId = this.SocketId;

                if (isSendHeader)
                {
                    data = PrepareHeader(type, partnerId, data);
                }
                Send(data);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "Error when sending data to remote server without specific length");
            }
        }
        private void Send(byte[] data)
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
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "Error when sending data to remote server without specific length");
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
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "SendCallback: socket error on socketid: "+ SocketId);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "SendCallback error on socketid: " + SocketId);
            }
        }
        private void Cancel()
        {
            _cts.Cancel();
        }
        public void Dispose()
        {
            SocketDisposing?.Invoke(this, new SocketDisposeEventArgs(SocketId));
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
                        _receivetasks.CompleteAdding();
                        foreach (var item in _receivetasks.GetConsumingEnumerable())
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
                    _sckConnect.Dispose();
                    _workAvailable.Dispose();
                }
            }
            _isDisposed = true;
        }
        #endregion
    }

}
