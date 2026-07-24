using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vsign4.VRemoteDesktop.DTOs;
using Vsign4.VRemoteDesktop.DTOs.Responses;
using Vsign4.VRemoteDesktop.Enums;
using Vsign4.VRemoteDesktop.Helpers;
using Vsign4.VRemoteDesktop.Presenters.Enums;
using Vsign4.VRemoteDesktop.Services.ScreenCapture.DTOs;
using Vsign4.VRemoteDesktop.Services.ScreenCapture.Enums;
using Vsign4.VRemoteDesktop.Services.ScreenCapture.GDI;
using Vsign4.VRemoteDesktop.Services.ScreenCapture.Utils;
using Vsign4.VRemoteDesktop.Services.SessionManagement.DTOs;
using Vsign4.VRemoteDesktop.Services.SessionManagement.Enums;
using Vsign4.VRemoteDesktop.Services.SessionManagement.Events.ClientSession;
using Vsign4.VRemoteDesktop.Services.SessionManagement.Events.ClientSocket;
using Vsign4.VRemoteDesktop.Utils;

namespace Vsign4.VRemoteDesktop.Services.SessionManagement
{
    public class ClientSession : IDisposable
    {
        private const long DELAY_TIME_PER_CHUNK_FILE = 50 * TimeSpan.TicksPerMillisecond;
        private const int TIMEOUT = 30;

        private readonly object _lock = new object();
        private readonly int _width;
        private readonly int _height;
        private readonly int _bytePerPixel;
        private readonly string _sessionId;
        private readonly PixelFormat _pixelFormat;
        private ClientType _sessionType;

        private bool _connected;
        private int _disposed = 0;
        private int _sending = 0;
        private byte[] _bufferPool;
        private DateTimeOffset _lastPing;
        private long _lastFileSend = Stopwatch.GetTimestamp();

        private readonly ClientSocket _clientSocket;
        private readonly VRegions _screenRegions;
        private readonly AutoResetEvent _sendWakeUp;
        private readonly CancellationTokenSource _cancelationTokenSource;

        private readonly HashSet<string> _cancelFile; // File had been cancelled, ignore queue have fileId in this
        private readonly ConcurrentQueue<QueueItem> _highQueue; // Keyboard, mouse, clipboard, message,...
        private readonly ConcurrentQueue<QueueItem> _lowQueue; // File send
        private readonly ConcurrentQueue<QueueItem> _receiveQueue; // Everything received

        private PartnerNetworkInfo _partnerInfo; // Information of client connect on this socket
        private Task _sendTask; // Handle send queue
        private Task _receiveTask; // Handle received queue
        private System.Threading.Timer _pingTimer; // last time ping success to server


        public event EventHandler<ClientSessionDataReceivedEventArgs> OnDataReceived;
        public event EventHandler<ClientSessionDisconnectedEventArgs> OnDisconnected;
        public event EventHandler<ClientSessionDataReceivedEventArgs> OnChatReceived;
        public event EventHandler<ClientSessionScreenReceivedEventArgs> OnScreenReceived;
        public ClientSession(string id, ClientType type, int width, int height, int bytePerPixel, PixelFormat pixelFormat)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentNullException("id");
            if (width <= 0)
                throw new ArgumentOutOfRangeException("Width cannot less than or equal zero");
            if (height <= 0)
                throw new ArgumentOutOfRangeException("Height cannot less than or equal zero");
            if (bytePerPixel <= 0)
                throw new ArgumentOutOfRangeException("bytePerPixel cannot less than or equal zero");

            _width = width;
            _height = height;
            _bytePerPixel = bytePerPixel;
            _pixelFormat = pixelFormat;
            _lastPing = DateTimeOffset.UtcNow;


            _sessionId = id;
            _sessionType = type;

            _cancelFile = new HashSet<string>();
            _highQueue = new ConcurrentQueue<QueueItem>();
            _lowQueue = new ConcurrentQueue<QueueItem>();
            _receiveQueue = new ConcurrentQueue<QueueItem>();

            _sendWakeUp = new AutoResetEvent(false);
            _cancelationTokenSource = new CancellationTokenSource();

            _clientSocket = new ClientSocket(id, _cancelationTokenSource.Token);
            if (_sessionType == ClientType.Controlled)
            {
                _screenRegions = new VRegions(_width, _height, _bytePerPixel);
                _bufferPool = VArrayPool.Rent((int)((_screenRegions.GetStride1(_width, _bytePerPixel) * _height) * 1.2));
            }
            else
            {
                _screenRegions = null;
                _bufferPool = VArrayPool.Rent(2 * (_bytePerPixel * _width * _height));
            }

            _sendTask = Task.Factory.StartNew(
                    () => SenderWorker(_cancelationTokenSource.Token),
                    _cancelationTokenSource.Token,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
            _receiveTask = Task.Factory.StartNew(
                    () => ReceivedWorker(_cancelationTokenSource.Token),
                    _cancelationTokenSource.Token,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);

            _clientSocket.OnConnected += OnSocketConnectedEventHandler;
            _clientSocket.OnDataReceived += OnDataReceivedEventHandler;
            _clientSocket.OnSendCompleted += OnSendCompletedEventHandler;
            _clientSocket.OnDisconnected += OnSocketDisconnectEventHandler;

            _pingTimer = new System.Threading.Timer(PingCallback, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
            ////use for server socket
            //if (_sessionType == ClientType.System)
            //{
            //    _pingTimer = new System.Threading.Timer(PingCallback, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
            //}
        }

        #region  Properties
        public PixelFormat PixelFormat
        {
            get
            {
                return _pixelFormat;
            }
        }
        public int BytePerPixel
        {
            get
            {
                return _bytePerPixel;
            }
        }
        public VBufferSwapper BufferSwapper
        {
            get
            {
                // _screenRegions is only allocated for Controlled sessions; a Controller
                // session has no capture buffer, so guard against a NullReferenceException.
                return _screenRegions == null ? null : _screenRegions.BufferSwapper;
            }
        }
        public bool Connected
        {
            get
            {
                lock (_lock)
                {
                    return _connected;
                }
            }
            set
            {
                lock (_lock)
                {
                    _connected = value;
                }
            }
        }
        public PartnerNetworkInfo PartnerInfo
        {
            get
            {
                lock (_lock)
                {
                    return _partnerInfo;
                }
            }
            set
            {
                lock (_lock)
                {
                    _partnerInfo = value;
                }
            }
        }
        public string SessionId
        {
            get
            {
                return _sessionId;
            }
        }
        public ClientType SessionType
        {
            get
            {
                return _sessionType;
            }
        }
        public ClientSocket Client
        {
            get
            {
                return _clientSocket;
            }
        }
        public VRegions ScreenRegions
        {
            get
            {
                return _screenRegions;
            }
        }
        private bool IsDisposed
        {
            get
            {
                return Interlocked.CompareExchange(ref _disposed, 0, 0) != 0;
            }
        }

        #endregion

        #region Workers
        public int GetStride(int width, int bytePerPixel)
        {
            return _screenRegions.GetStride1(width, bytePerPixel);
        }
        private void PingCallback(object obj)
        {            
            if (_sessionType != ClientType.System || (_sessionType == ClientType.Controller && !_connected) || (_sessionType == ClientType.Controlled && !_connected))
                return;
            var elapsed = (DateTimeOffset.UtcNow - _lastPing).TotalSeconds;

            if (elapsed > TIMEOUT)
            {
                OnSocketDisconnectEventHandler(this, new SocketDisconnectedEventArgs(this._sessionId));
                return;
            }
            //_client.SendPing();
            AddWork(QueuePriority.High, new TaskObject(SocketDataType.Ping, _sessionId, new byte[0], true));
        }
        private void SenderWorker(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    bool hasWork = false;
                    if (_highQueue.Count > 0)
                    {
                        if (HighQueueHandler())
                            hasWork = true;
                    }
                    else if (_screenRegions != null && _screenRegions.HasData && _screenRegions.ReadyToSend())
                    {
                        if (DirtyRegionSend())
                            hasWork = true;
                    }
                    else if (_lowQueue.Count > 0)
                    {
                        if (LowQueueHandler())
                            hasWork = true;
                    }
                    if (!hasWork)
                    {
                        //Wait
                        _sendWakeUp.WaitOne(100);
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch
                {
                    //Write log
                    Thread.Sleep(100);
                }
            }
        }
        private bool HighQueueHandler()
        {
            QueueItem highTask;
            if (!_highQueue.TryPeek(out highTask)) return false;

            var taskObject = highTask.Data as TaskObject;
            if (taskObject == null)
            {
                _highQueue.TryDequeue(out highTask);
                return true; //continue immediately
            }
            bool sentSuccessfully = false;

            if (taskObject.TaskType == SocketDataType.ScreenSend && taskObject.CapturedFrame != null)
            {
                sentSuccessfully = Send(taskObject.CapturedFrame);
            }
            else
            {
                sentSuccessfully = Send(taskObject.TaskType, taskObject.Data, taskObject.SessionId, taskObject.IsSendHeader);
            }

            taskObject.Attempts++;
            if (sentSuccessfully || taskObject.Attempts  >= 3)
            {
                _highQueue.TryDequeue(out highTask);
                return true;
            }
            return false;
        }
        private bool LowQueueHandler()
        {
            var now = Stopwatch.GetTimestamp();
            if ((now - _lastFileSend) > DELAY_TIME_PER_CHUNK_FILE)
            {
                lock (_lock)
                {

                    QueueItem queueItem;
                    if (!_lowQueue.TryPeek(out queueItem)) return false;

                    var taskObject = queueItem.Data as TaskObject;
                    if (taskObject == null)
                    {
                        _highQueue.TryDequeue(out queueItem);
                        return true; //continue immediately
                    }

                    bool sentSuccessfully = false;

                    if (_cancelFile.Contains(taskObject.ChunkFileInfo.FileId))
                        return true; //ignore because file with this id had been cancelled

                    sentSuccessfully = ProcessFileTransfer(taskObject);

                    taskObject.Attempts++;
                    if (sentSuccessfully || taskObject.Attempts >= 3)
                    {
                        _lowQueue.TryDequeue(out queueItem);
                        return true;
                    }
                }
            }
            return false;
        }
        private void ReceivedWorker(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {

                try
                {
                    bool hasWork = false;

                    QueueItem task; 
                    if (_receiveQueue.TryDequeue(out task))
                    {
                        ReceivedQueueHandler(task);
                        hasWork = true;

                    }

                    if (!hasWork)
                    {
                        _sendWakeUp.WaitOne(100);
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch
                {
                    //Write log
                    Thread.Sleep(100);
                }
            }
        }

        private void ReceivedQueueHandler(QueueItem task)
        {
            var e = task.Data as SocketDataReceivedEventArgs;
            if (e != null)
            {
                switch (e.Type)
                {
                    case SocketDataType.ScreenOk:
                        FullScreenSendCompleted();
                        //OnDataReceived.Invoke(this, new ClientSessionDataReceivedEventArgs(sessionId: this.SessionId, type: e.Type, data: e.Data));
                        break;
                    case SocketDataType.RegionsChangedOk:
                        ScreenCompleted();
                        break;
                    case SocketDataType.ScreenSend:
                        FullScreenHandler(e);
                        break;
                    case SocketDataType.ScreenRegionsChangedSend:
                        DirtyRegionHandler(e);
                        break;
                    case SocketDataType.ChatSend:
                        if(OnChatReceived != null)
                            OnChatReceived.Invoke(this, new ClientSessionDataReceivedEventArgs(this._sessionId, e.Type, e.Data));
                        break;
                    case SocketDataType.Disconnect:
                    case SocketDataType.RemoteControlDisconnect:
                        Close();
                        break;
                    case SocketDataType.Pong:
                        _lastPing = DateTimeOffset.UtcNow;
                        break;
                    default:
                        if (OnDataReceived != null)
                            OnDataReceived.Invoke(this, new ClientSessionDataReceivedEventArgs(sessionId: this.SessionId, type: e.Type, data: e.Data));
                        break;

                }
            }

        }
        private void FullScreenHandler(SocketDataReceivedEventArgs e)
        {
            AddWork(QueuePriority.High,
                new TaskObject(SocketDataType.ScreenOk, this._sessionId, new byte[0], true));

            if(OnScreenReceived != null)
                OnScreenReceived.Invoke(this, new ClientSessionScreenReceivedEventArgs(SessionManagement.Events.ClientSession.ScreenType.FullScreen, e.Data));
        }
        private void DirtyRegionHandler(SocketDataReceivedEventArgs e)
        {
            AddWork(QueuePriority.High,
                new TaskObject(SocketDataType.RegionsChangedOk, this._sessionId, new byte[0], true));

            if(OnScreenReceived != null)
                OnScreenReceived.Invoke(this, new ClientSessionScreenReceivedEventArgs(SessionManagement.Events.ClientSession.ScreenType.DirtyRegions, e.Data));
        }
        #endregion

        #region VClient
        public void AddWork(QueuePriority priority, params TaskObject[] taskObjects)
        {
            AddWork(priority, taskObjects.ToList());
        }
        public void AddWork(QueuePriority priority, List<TaskObject> taskObjects)
        {
            taskObjects.ForEach(x => AddWork(priority, x));
        }
        public void AddWork(QueuePriority priority, TaskObject task)
        {
            if (task == null) return;
            if (IsDisposed) return;

            if (priority == QueuePriority.High)
            {
                _highQueue.Enqueue(new QueueItem(task));
            }
            else if (priority == QueuePriority.Low)
            {
                if (task.ChunkFileInfo == null) return;

                _lowQueue.Enqueue(new QueueItem(task));
            }
            //Add queue
            _sendWakeUp.Set();
        }
        public bool TryConnect(string ip, int port, int retry = 0, int timeout = 3000)
        {
            return _clientSocket.TryConnect(ip, port, retry, timeout);
        }
        public bool Listen(int port, int timeout = 3000)
        {
            if (port <= 0) throw new ArgumentNullException("Port cannot be less than or equal zero");

            return _clientSocket.Listen(port, timeout);
        }
        private bool Send(CapturedFrame frame)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(this.GetType().Name);
            try
            {
                Sendstate state = new Sendstate
                {
                    Data = frame.CompressedData,
                    Remained = frame.CompressedDataLength,
                    Sent = frame.CompressedDataOffset,
                    Timeout = Environment.TickCount,
                    RentBuffer = true
                };
                return SendState(state);
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "Error when sending data to remote server without specific length");
                return false;
            }
        }
        private bool Send(SocketDataType type, byte[] data, string id = null, bool sendHeader = true)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 1) != 0)
                return false;
            try
            {
                if (data == null || data.Length <= 0)
                    data = new byte[0];

                if (sendHeader)
                {
                    //data = HeaderGenerate(type: type, id: _sessionId, true, data);
                    HeaderGenerate(type: type, sessionId: _sessionId, dataLength: data.Length, buffer: _bufferPool, offset: 0);
                    Buffer.BlockCopy(data, 0, _bufferPool, 13, data.Length);

                    int length = 13 + data.Length;

                    Sendstate state = new Sendstate
                    {
                        Data = _bufferPool,
                        Remained = length,
                        Sent = 0,
                        Timeout = Environment.TickCount,
                        RentBuffer = false,
                    };
                    return SendState(state);
                }
                else
                {
                    Sendstate state = new Sendstate
                    {
                        Data = data,
                        Remained = data.Length,
                        Sent = 0,
                        Timeout = Environment.TickCount,
                        RentBuffer = false,
                    };
                    return SendState(state);
                }
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "Error when sending data to remote server without specific length");
                return false;
            }
        }
        //private bool Send(SocketDataType type, byte[] data, string id = null, bool sendHeader = true)
        //{
        //    if (Interlocked.CompareExchange(ref _disposed, 1, 1) != 0)
        //        return false;

        //    try
        //    {
        //        if (data.Length <= 0)
        //            data = new byte[0];

        //        if (sendHeader)
        //        {
        //            data = HeaderGenerate(type: type, id: _sessionId, true, data);
        //        }
        //        Sendstate state = new Sendstate
        //        {
        //            Data = data,
        //            Remained = data.Length,
        //            Sent = 0,
        //            Timeout = Environment.TickCount,
        //            RentBuffer = false,
        //        };
        //        return SendState(state);
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "Error when sending data to remote server without specific length");
        //        return false;
        //    }
        //}
        private bool SendState(Sendstate state)
        {
            if (Interlocked.CompareExchange(ref _sending, 1, 0) != 0)
                return false;

            _clientSocket.Send(state);
            return true;
        }

        #endregion

        #region ScreenRegion
        public void ScreenReceived(VScreenType type)
        {
            if (type == VScreenType.FullScreen)
            {
                AddScreen();
            }
            else
            {
                _screenRegions.SetHasData();
            }
        }
        private void FullScreenSendCompleted()
        {
            _screenRegions.SetFullScreenCompleted();
        }
        private void ScreenCompleted()
        {
            _screenRegions.SendCompleted();
        }
        public void AddScreen()
        {
            SendCapture(SocketDataType.ScreenSend, VScreenType.FullScreen);
        }
        private bool DirtyRegionSend()
        {
            return SendCapture(SocketDataType.ScreenRegionsChangedSend, VScreenType.DirtyRegions);
        }
        private bool SendCapture(SocketDataType type, VScreenType screenType)
        {
            var dirtyRegions = _screenRegions.GetData(screenType);
            if (dirtyRegions == null)
            {
                _screenRegions.SendCompleted();
                return false;
            }
            try
            {
                //13 bytes for header
                int dataOffset = 13;

                //Check if bufferPoll smaller than requireSize -> return current bufferPool to VArrayPool and rent new buffer
                int requiredSize = Compressor.GetMaxOutputLength(dirtyRegions.Length) + 13;
                if (_bufferPool.Length < requiredSize)
                {
                    if (_bufferPool != null)
                    {
                        VArrayPool.Return(_bufferPool);
                    }

                    _bufferPool = VArrayPool.Rent(requiredSize);
                }
                int length = Compressor.CompressedLZ4(dirtyRegions.Buffer, dirtyRegions.Length, _bufferPool, dataOffset, _bufferPool.Length - dataOffset);

                var header = HeaderGenerate(
                    type: type,
                    sessionId: this._sessionId,
                    dataLength: length,
                    buffer: _bufferPool,
                    offset: 0);

                var frame = new CapturedFrame(ScreenCapture.Enums.VScreenType.DirtyRegions, _bufferPool, 0, length + 13);

                var result = Send(frame);
                if (result)
                {
                    _screenRegions.ReadCompleted();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                VArrayPool.Return(dirtyRegions.Buffer);
            }
        }
        #endregion

        #region Methods
        public void Close()
        {
            var handler = OnDisconnected;
            if (handler != null)
                handler.Invoke(this, new ClientSessionDisconnectedEventArgs(_sessionId));
        }
        public void UpdatePartnerInfo(PartnerNetworkInfo partnerInfo)
        {
            if (partnerInfo == null) throw new ArgumentNullException("partner info");
            _partnerInfo = partnerInfo;
        }
        private int HeaderGenerate(SocketDataType type, string sessionId, int dataLength, byte[] buffer, int offset)
        {
            try
            {
                //Phần header sẽ chứa 13 byte 4 bytes đầu: là tổng độ dài packet, 8 bytes tiếp là sessionId và byte cuối là data type
                int headerOnlySize = HeaderSchema.PayloadBytes + HeaderSchema.TypeBytes + sessionId.Length;

                int totalMessageSize = headerOnlySize + dataLength;

                Buffer.BlockCopy(BitConverter.GetBytes(totalMessageSize), 0, buffer, offset, HeaderSchema.PayloadBytes);
                offset += HeaderSchema.PayloadBytes;

                buffer[offset] = (byte)type;
                offset += HeaderSchema.TypeBytes;

                byte[] idByteArray = ByteArrayHelper.ConvertStringToByteArray(sessionId, EncodingType.ASCII).GetResult();
                Buffer.BlockCopy(idByteArray, 0, buffer, offset, idByteArray.Length);
                offset += idByteArray.Length;

                return offset;
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", GetType().Name).Error(ex, "Generate header error ");
                return offset;
            }
        }
        private bool ProcessFileTransfer(TaskObject task)
        {
            if (task.ChunkFileInfo == null)
            {
                //Send metadata
                return Send(task.TaskType, task.Data, task.SessionId, task.IsSendHeader);
            }
            //Send file data
            try
            {
                FileHelper.OpenStream(task.ChunkFileInfo.FilePath);
                int headerSize = HeaderSchema.TypeBytes + HeaderSchema.PayloadBytes + HeaderSchema.FileIdBytes;

                byte[] chunkFileData = new byte[task.ChunkFileInfo.ChunkSize + headerSize];

                if (!Enum.IsDefined(typeof(ChatDataType), (int)task.Data[0]))
                {
                    return true; //drop
                }
                int offset = 0;
                //Data type
                ChatDataType type = (ChatDataType)task.Data[0];
                chunkFileData[offset] = (byte)type;
                offset += HeaderSchema.TypeBytes;

                //Chunk offset
                Buffer.BlockCopy(BitConverter.GetBytes(task.ChunkFileInfo.Offset), 0, chunkFileData, offset, HeaderSchema.PayloadBytes);
                offset += HeaderSchema.PayloadBytes;

                //File Id
                Buffer.BlockCopy(Encoding.ASCII.GetBytes(task.ChunkFileInfo.FileId), 0, chunkFileData, offset, HeaderSchema.FileIdBytes);
                offset += HeaderSchema.FileIdBytes;

                //File data
                int chunkRead = FileHelper.CopyFileDataByOffset(task.ChunkFileInfo.FilePath, task.ChunkFileInfo.Offset, ref chunkFileData, offset, task.ChunkFileInfo.ChunkSize);
                if (chunkRead != chunkFileData.Length - headerSize)
                {
                    RemoveFile(task.ChunkFileInfo.FileId);
                    return true; //drop
                }
                if ((task.ChunkFileInfo.Offset + task.ChunkFileInfo.ChunkSize) >= task.ChunkFileInfo.FileLength)
                {
                    bool result = FileHelper.CloseStream(task.ChunkFileInfo.FilePath);
                    if (!result)
                        Logger.Log.ForContext("FileName", this.GetType().Name).Error("Close stream failed");
                }
                return Send(task.TaskType, chunkFileData, task.SessionId, task.IsSendHeader);
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "Send chunk file on socket id " + this._sessionId + "error ");
                return false;
            }
        }
        public void RemoveFile(string fileId)
        {
            _cancelFile.Add(fileId);
        }
        private void SendDisconnectedNotification()
        {
            try
            {
                if (_clientSocket == null)
                    return;

                // Build a header-only Disconnect packet and push it to the partner
                // synchronously. This must NOT go through the async Send(...)/queue path:
                // by the time we notify, the session is being torn down (worker tasks
                // cancelled, _disposed set), so a blocking best-effort send on the still
                // open socket is the only reliable way to let the P2P partner know.
                byte[] buffer = new byte[HeaderSchema.TotalHeaderSize];
                HeaderGenerate(SocketDataType.Disconnect, _sessionId, 0, buffer, 0);
                _clientSocket.SendSync(buffer);
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", "ClientSession").Error(ex, "SendDisconnectedNotification err");
            }
        }
        #endregion

        #region Events
        private void OnSocketConnectedEventHandler(object sender, SocketConnectedEventArgs e)
        {
            if (OnDataReceived != null)
                OnDataReceived.Invoke(this, new ClientSessionDataReceivedEventArgs(sessionId: this.SessionId, type: SocketDataType.Connect,data: null, isSuccess: e.Connected));
        }
        private void OnDataReceivedEventHandler(object sender, SocketDataReceivedEventArgs e)
        {
            _receiveQueue.Enqueue(new QueueItem(e));
        }
        private void OnSendCompletedEventHandler(object sender, SocketSendCompletedEventArgs e)
        {
            Interlocked.Exchange(ref _sending, 0);
        }
        private void OnSocketDisconnectEventHandler(object sender, SocketDisconnectedEventArgs e)
        {
            var handler = OnDisconnected;
            if (handler != null)
                handler.Invoke(this, new ClientSessionDisconnectedEventArgs(_sessionId));
        }
        #endregion

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;

            try
            {
                if (disposing)
                {
                    if (_cancelationTokenSource != null)
                        _cancelationTokenSource.Cancel();

                    try
                    {
                        // Dispose is frequently triggered from inside one of these worker
                        // threads (peer Disconnect packet -> ReceivedWorker -> Close -> Remove
                        // -> Dispose). Waiting on the current task would just self-deadlock
                        // until the 3s timeout, so exclude the task we are running on.
                        int? currentTaskId = Task.CurrentId;
                        var tasksToWait = new[] { _sendTask, _receiveTask }
                            .Where(t => t != null && t.Id != (currentTaskId ?? -1))
                            .ToArray();

                        if (tasksToWait.Length > 0)
                            Task.WaitAll(tasksToWait, TimeSpan.FromSeconds(3));
                    }
                    catch (AggregateException) { }
                    catch (TaskCanceledException) { }

                    _cancelFile.Clear();
                    QueueItem item; 
                    while (_highQueue.TryDequeue(out item)) { }
                    while (_lowQueue.TryDequeue(out item)) { }
                    while (_receiveQueue.TryDequeue(out item)) { }

                    try
                    {
                        SendDisconnectedNotification();
                    }
                    catch { }

                    if (_clientSocket != null)
                        _clientSocket.Dispose();

                    if (_screenRegions != null)
                        _screenRegions.Dispose();

                    if (_pingTimer != null)
                    {
                        _pingTimer.Change(Timeout.Infinite, Timeout.Infinite);
                        _pingTimer.Dispose();
                    }

                    _cancelationTokenSource.Dispose();
                }
            }
            finally
            {
                var buf = _bufferPool;
                if (buf != null)
                {
                    _bufferPool = null;
                    VArrayPool.Return(buf);
                }
            }
        }
        ~ClientSession()
        {
            Dispose();
        }
    }
}
