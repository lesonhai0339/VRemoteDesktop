using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Models;
using VRemoteDesktop.Utils;
using VRemoteServer.Models;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;

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

        private readonly BlockingCollection<DataReceive> _receivedQueue;
        private readonly ICusQueue<object> _senderQueue;
        // private readonly VPriorityQueue<object, int> _senderTasks;

        public event EventHandler<SocketDisposeEventArgs> SocketDisposing;
        public event EventHandler<P2PClientDataReceived> TCPClientReceived;
        public event EventHandler<P2PScreenEventArgs> P2PScreenReceived;
        public event EventHandler<P2PChatEventArgs> P2PChatReceived;

        private System.Threading.Timer _timer;
        private int bytesPerSecond;
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

            _senderQueue = new CusQueue<object>();
            _receivedQueue = new BlockingCollection<DataReceive>();
            //_senderTasks = new VPriorityQueue<object, int>();

            ReceivedWorker = new BackgroundWorker();
            ReceivedWorker.WorkerSupportsCancellation = true;
            SenderWorker = new BackgroundWorker();
            SenderWorker.WorkerSupportsCancellation = true;
            if (!SenderWorker.IsBusy)
            {
                SenderWorker.RunWorkerAsync();
            }
            bytesPerSecond = 0;
            _timer = new System.Threading.Timer(BandWidth, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        private void BandWidth(object state)
        {
            lock (_lockObject)
            {
                double bandWidth = (bytesPerSecond * 8) * 1.0 / 1000000; 
                if(bandWidth > 0)
                    Logger.Log.ForContext("", this.GetType().Name + "_BandWidth").Info(string.Format("{0} - {1} Mbps",this.SocketId, bandWidth));
                bytesPerSecond = 0;
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
                foreach (var task in _receivedQueue.GetConsumingEnumerable(_cancellationToken))
                {
                    int start = Environment.TickCount;
                    try
                    {
                        if (task.Type == SocketDataType.Screen || task.Type == SocketDataType.Chunks)
                        {
                            P2PScreenReceived?.Invoke(this, new P2PScreenEventArgs(task.Type, task.Data));
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
                        Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "DoWork error");
                    }
                    int end = Environment.TickCount - start;
                    Logger.Log.ForContext("FileName", this.GetType().Name + "DataReceivedWork").Info(string.Format("Elapsed: {0} - {1}\n", end, DateTime.Now.ToString("HH:mm:ss:fff")));
                }
            }
            catch(OperationCanceledException ex)
            {
                Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "DataReceivedWork error");
            }
        }
        private void SenderDoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                while (!_cancellationToken.IsCancellationRequested)
                {
                    //if (_senderTasks.Dequeue(out var taskObj))
                    if (_senderQueue.Dequeue(out var taskObj))
                    {
                        try
                        {
                            if (taskObj is TaskGroup taskGroup)
                            {
                                int length = taskGroup.Tasks.Count;
                                for(int i= 0; i < length; i++)
                                {
                                    ProcessTask(taskGroup.Tasks[i]);
                                }                              
                            }
                            else if (taskObj is TaskObject task)
                            {
                                ProcessTask(task);
                            }

                        }
                        catch (Exception ex)
                        {
                            Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "DoWork error");
                        }
                    }
                    Thread.Sleep(10);
                }
            }
            catch (OperationCanceledException ex)
            {
                Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "DataReceivedWork error");
            }
        }
        private void ProcessTask(TaskObject task)
        {
            if (task.TaskType == SocketDataType.Chat)
            {
                ProcessFileTransfer(task);
                return;
            }
            Send(task.TaskType, task.Data, task.SessionId, task.IsSendHeader);
        }
        public void RemoveTaskByType(SocketDataType socketType, object dataType, object data)
        {
            if (socketType == SocketDataType.None)
            {
                Logger.Log.ForContext("FileName", this.GetType().Name).Error("SocketDataType is none, pass");
                return;
            }
            if (dataType == null)
            {
                Logger.Log.ForContext("FileName", this.GetType().Name).Error("dataType is null, pass");
                return;
            }
            if (data == null)
            {
                Logger.Log.ForContext("FileName", this.GetType().Name).Error("data is null, pass");
                return;
            }
            if (socketType == SocketDataType.Chat)
            {
                if(dataType is ChatDataType chat && chat == ChatDataType.StopReceivedFileData)
                {
                    if(data is string fileId)
                    {
                        int removed = _senderQueue.RemoveAll(QueuePriority.Low,item =>
                        {
                            if (item is TaskObject task)
                            {
                                if (task.TaskType == SocketDataType.Chat)
                                {
                                    ChatDataType chatType = (ChatDataType)task.Data[0];
                                    if(chatType == ChatDataType.FileData)
                                    {
                                        string id = task.ChunkFileInfo.FileId;
                                        return id == fileId;
                                    }
                                }
                            }
                            return false;
                        });
                    }
                }             
            }     
        }
        public void AddWork(TaskObject task, QueuePriority priority)
        {
            if (task == null) return;
            _senderQueue.Enqueue(task, priority);
            //_senderTasks.Enqueue(task, (int)task.Priority);
        }
        public void AddWorkGroup(List<TaskObject> tasks, QueuePriority priority)
        {
            if (tasks == null || tasks.Count == 0) return;

            _senderQueue.Enqueue(new TaskGroup(tasks), priority);
            //_senderTasks.Enqueue(new TaskGroup(tasks), (int)tasks[0].Priority);
        }
        public void AddWorkGroup(TaskObject[] tasks, QueuePriority priority)
        {
            if (tasks == null || tasks.Length == 0) return;
            _senderQueue.Enqueue(new TaskGroup(tasks), priority);
            //_senderTasks.Enqueue(new TaskGroup(tasks), (int)tasks[0].Priority);
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
                if (string.IsNullOrWhiteSpace(ip) || port < 0)
                {
                    Logger.Log.ForContext("FileName", nameof(Connect)).Error("Invalidate argument at Connect method");
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
                    Logger.Log.ForContext("FileName", nameof(Connect)).Error("Invalid IP address: {Ip}", ip);
                }
            }
            catch (SocketException ex)
            {
                Logger.Log.ForContext("FileName", nameof(Connect)).Error(ex, "Error when connect to relay server");
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", nameof(Connect)).Error(ex, "Unexpected error when connect to relay server");
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
                _sckConnect.Set();
                Socket.EndConnect(ar);
                if (!Socket.Connected)
                {
                    //Connected?.Invoke(this, new ConnectEventArgs(false));
                    TCPClientReceived?.Invoke(this, new P2PClientDataReceived(SocketDataType.Connect, false, new byte[0]));
                    Logger.Log.ForContext("FileName", this.GetType().Name).Error("Cannot connect to server");
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
            }
            catch (SocketException ex)
            {
                Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "SocketException when connecting to remote server");
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "Unexpected error when connecting to remote server");
            }
        }
        public void UpdatePartnerInfo(ClientInfo partnerInfo)
        {
            if(partnerInfo == null)
            {
                Logger.Log.ForContext("FileName", this.GetType().Name).Error("Missing some partner value, dispose VClient with id: "+ SocketId);
                //Info invalid, dispose this class
                this.Dispose();
            }
            else
            {
                Partner = partnerInfo;
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

                        int length = BitConverter.ToInt32(stateObject.ByteArrayBuilder.lsByte.GetRange(0, ByteConstants.INT32_LENGTH).ToArray(), 0);
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
                    Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "Begin receive error");
                }
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "Unexpected error when receiving data from remote server");
            }
        }
        private void ProcessReceiveData(byte[] bytes)
        {
            try
            {
                int headerSize = ByteConstants.INT32_LENGTH + RandomLength.SOCKET_ID_LENGTH + RandomLength.DATA_TYPE_LENGTH;

                if (bytes.Length < headerSize)
                {
                    Logger.Log.ForContext("", this.GetType().Name).Error("Data received less than header size, not handler");
                    return;
                }
                int offset = 0;

                int dataLength = BitConverter.ToInt32(bytes, offset);
                if(dataLength <= 0)
                {
                    Logger.Log.ForContext("", this.GetType().Name).Error("Data length mismatch");
                    return;
                }
                offset += ByteConstants.INT32_LENGTH;

                SocketDataType dataType = (SocketDataType)bytes[offset];
                if(!Enum.IsDefined(typeof(SocketDataType), dataType))
                {
                    Logger.Log.ForContext("", this.GetType().Name).Error("Invalid SocketDataType");
                    return;
                }
                offset += RandomLength.DATA_TYPE_LENGTH;

                var result = ByteArrayHelper.ConvertByteArrayToString(bytes, offset, RandomLength.SOCKET_ID_LENGTH, EncodingType.ASCII);
                if (!result.IsSuccess)
                {
                    Logger.Log.ForContext("", this.GetType().Name).Error("Cannot convert byte array to string");
                    return;
                }
                string socketId = result.GetResult();
                offset += RandomLength.SOCKET_ID_LENGTH;

                byte[] data = new byte[bytes.Length - headerSize];
                Buffer.BlockCopy(bytes, offset, data, 0, data.Length);

                _receivedQueue.Add(new DataReceive
                (
                    type: dataType,
                    length: dataLength,
                    data: data,
                    socketId: socketId
                ));
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("", GetType().Name).Error(ex, "ProcessReceiveData error");
            }
        }
        public byte[] HeaderGenerate(SocketDataType type, string socketId, bool includeData = false, byte[] data = null, int dataSize = 0)
        {
            try
            {
                int headerOnlySize = RandomLength.DATA_TYPE_LENGTH + ByteConstants.INT32_LENGTH + socketId.Length;
                int actualDataSize = includeData ? data.Length : dataSize;
                int totalMessageSize = headerOnlySize + actualDataSize;
                int headerSize = includeData ? totalMessageSize : headerOnlySize;

                byte[] header = new byte[headerSize];
                int offset = 0;

                Buffer.BlockCopy(BitConverter.GetBytes(totalMessageSize), 0, header, offset, ByteConstants.INT32_LENGTH);
                offset += ByteConstants.INT32_LENGTH;

                header[offset] = (byte)type;
                offset += RandomLength.DATA_TYPE_LENGTH;

                byte[] idByteArray = ByteArrayHelper.ConvertStringToByteArray(socketId, EncodingType.ASCII).GetResult();
                Buffer.BlockCopy(idByteArray, 0, header, offset, idByteArray.Length);
                offset += idByteArray.Length;

                if (includeData)
                {
                    Buffer.BlockCopy(data, 0, header, offset, data.Length);
                    offset += data.Length;
                }
                return header;
            }
            catch(Exception ex)
            {
                Logger.Log.ForContext("FileName", GetType().Name).Error(ex, "Generate header error ");
                return null;
            }
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
                try
                {
                    int headerSize = RandomLength.DATA_TYPE_LENGTH + ByteConstants.INT32_LENGTH + RandomLength.FILE_ID_LENGTH;

                    byte[] chunkFileData = new byte[task.ChunkFileInfo.ChunkSize + headerSize];

                    if (!Enum.IsDefined(typeof(ChatDataType), (int)task.Data[0]))
                    {
                        Logger.Log.ForContext("FileName", this.GetType().Name).Error("Invalid ChatDataType, not handler");
                        return;
                    }
                    int offset = 0;
                    //Data type
                    ChatDataType type = (ChatDataType)task.Data[0];
                    chunkFileData[offset] = (byte)type;
                    offset += RandomLength.DATA_TYPE_LENGTH;

                    //Chunk offset
                    Buffer.BlockCopy(BitConverter.GetBytes(task.ChunkFileInfo.Offset), 0, chunkFileData, offset, ByteConstants.INT32_LENGTH);
                    offset += ByteConstants.INT32_LENGTH;

                    //File Id
                    Buffer.BlockCopy(Encoding.ASCII.GetBytes(task.ChunkFileInfo.FileId), 0, chunkFileData, offset, RandomLength.FILE_ID_LENGTH);
                    offset += RandomLength.FILE_ID_LENGTH;

                    //File data
                    int chunkRead = FileHelper.GetChunkFileDataByOffset(task.ChunkFileInfo.FilePath, task.ChunkFileInfo.Offset, ref chunkFileData, offset, task.ChunkFileInfo.ChunkSize);

                    if (chunkRead != chunkFileData.Length - headerSize)
                    {
                        Logger.Log.ForContext("FileName", this.GetType().Name).Error("Error when ProcessFileTransfer send file data error, remove remain send file task");
                        RemoveTaskByType(task.TaskType, type, task.ChunkFileInfo.FileId);
                        return;
                    }
                    Send(task.TaskType, chunkFileData, task.SessionId, task.IsSendHeader);
                }
                catch(Exception ex)
                {
                    Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "Send chunk file on socket id "+ this.SocketId + "error ");
                }
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
                if (type == SocketDataType.None)
                    throw new ArgumentException("Missing arguments");
                
                //if (data == null)
                //    throw new ArgumentException("Missing arguments");

                if (string.IsNullOrWhiteSpace(partnerId))
                    partnerId = this.SocketId;


                if (isSendHeader)
                {
                    data = HeaderGenerate(type: type,socketId: partnerId, true, data);
                }
                Send(data);
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "Error when sending data to remote server without specific length");
            }
        }
        private void Send(byte[] data)
        {
            try
            {
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
                Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "Error when sending data to remote server without specific length");
            }
        }
        private void Send(Sendstate state)
        {
            if (!Socket.Connected)
            {
                throw new InvalidOperationException("Socket with id: "+ SocketId + " no available");
            }
            if (DateTime.Now.Subtract(state.Timeout).TotalSeconds > DefaultValue.DEFAULT_TIMEOUT_SECONDS)
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
                    lock (_lockObject)
                    {
                        bytesPerSecond += num;
                    }
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
                Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "SendCallback: socket error on socketid: "+ SocketId);
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "SendCallback error on socketid: " + SocketId);
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
            if (disposing)
            {
                if (_isDisposed) return;
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
                _receiveBackgroundWorker.CancelAsync();

                _receiveBackgroundWorker.DoWork -= DataReceivedWork;
                _receiveBackgroundWorker.Dispose();


                //background worker
                _senderBackgroundWorker.CancelAsync();

                _senderBackgroundWorker.DoWork -= SenderDoWork;
                _senderBackgroundWorker.Dispose();

                //queue
                if (_receivedQueue != null)
                {
                    _receivedQueue.CompleteAdding();
                    foreach (var item in _receivedQueue.GetConsumingEnumerable())
                    {
                        if (item is IDisposable disposableItem)
                        {
                            disposableItem.Dispose();
                        }
                    }
                }
                if (_senderQueue != null)
                {
                    _senderQueue.Dispose();
                }
                lock (_lockObject)
                {
                    try
                    {
                        Send(SocketDataType.Disconnect, new byte[0], null, true);
                        Thread.Sleep(50);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log.ForContext("", this.GetType().Name).Error(ex, "Send disconnection at dispose error: ");
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
            _isDisposed = true;
        }
        #endregion
    }

}
