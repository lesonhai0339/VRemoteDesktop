using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VRemoteDesktop.DTOs.Response;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.Machine.DTOs;
using VRemoteDesktop.Services.ScreenCapture.DTOs;
using VRemoteDesktop.Services.ScreenCapture.GDI;
using VRemoteDesktop.Services.SessionManagement;
using VRemoteDesktop.Services.SessionManagement.DTOs;
using VRemoteDesktop.Services.SessionManagement.Enums;
using VRemoteDesktop.Services.SessionManagement.Events;
using VRemoteDesktop.Services.SessionManagement.Events.ClientSession;
using VRemoteDesktop.Services.VTCPClient;
using VRemoteDesktop.Services.VTCPClient.Events;
using VRemoteDesktop.Utils;
using VRemoteServer.Models;

namespace VRemoteDesktop.Services.RemoteDesktop
{
    public class ClientSession : IDisposable
    {
        private readonly object _lock = new object();
        private const int TIME_OUT = 30;
        private int _disposed;
        private string _sessionId;
        private ClientType _sessionType;
        private bool _connected;


        private readonly int _width;
        private readonly int _height;
        private readonly int _bytePerPixel;

        private int _sending = 0;
        private long _lastSent = Stopwatch.GetTimestamp();

        private DateTimeOffset _lastPing;
        private byte[] _bufferPool;
        private long _lastSendTimestamp = Stopwatch.GetTimestamp();

        private PartnerNetworkInfo _partnerInfo;


        private System.Threading.Timer _pingTimer;
        private Task _sendTask;
        private Task _receiveTask;

        private readonly ConcurrentQueue<QueueItem> _highQueue; //Keyboard, mouse, clipboard,...
        private readonly ConcurrentQueue<QueueItem> _mediumQueue; //Screen, DirtyRegions

        private readonly HashSet<string> _cancelFile;
        private readonly ConcurrentQueue<QueueItem> _lowQueue; //File

        private readonly ConcurrentQueue<QueueItem> _receiveQueue;


        private readonly ClientSocket _clientSocket;
        private readonly VRegions _screenRegions;

        private AutoResetEvent _sendWakeUp;
        private AutoResetEvent _receivedWakeUp;

        private CancellationTokenSource _cancelationTokenSource;


        public event EventHandler<ClientSessionDataReceivedEventArgs> OnDataReceived;
        public event EventHandler<ClientSessionDisconnectedEventArgs> OnDisconnected;

        //still not implement, using after
        public event EventHandler<EventArgs> OnChatReceived;
        public event EventHandler<ClientSessionScreenReceivedEventArgs> OnScreenReceived;
        public ClientSession(string id, ClientType type, int width, int height, int bytePerPixel = 3)
        {
            if (string.IsNullOrEmpty(id)) 
                throw new ArgumentNullException("id");
            if (width <= 0)
                throw new ArgumentOutOfRangeException("Width cannot less than or equal zero");
            if (height <= 0)
                throw new ArgumentOutOfRangeException("Height cannot less than or equal zero");

            _width = width;
            _height = height;
            _bytePerPixel = bytePerPixel;

            _lastPing = DateTimeOffset.UtcNow;


            _sessionId = id;
            _sessionType = type;
            _highQueue = new ConcurrentQueue<QueueItem>();
            _mediumQueue = new ConcurrentQueue<QueueItem>();


            _cancelFile = new HashSet<string>();
            _lowQueue = new ConcurrentQueue<QueueItem>();

            _receiveQueue = new ConcurrentQueue<QueueItem>();

            _sendWakeUp = new AutoResetEvent(false);
            _receivedWakeUp = new AutoResetEvent(false);
            _cancelationTokenSource = new CancellationTokenSource();

            _clientSocket = new ClientSocket(id, _cancelationTokenSource.Token);

            if(_sessionType == ClientType.Controlled)
            {
                _screenRegions = new VRegions(_width, _height, _bytePerPixel);
                _bufferPool = VArrayPool.Rent((int)((_screenRegions.GetStride1(_width, _bytePerPixel) * _height) * 1.2));
            }
            else
            {
                _screenRegions = null;
                _bufferPool = VArrayPool.Rent(5 * 1024 * 1024);
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
            _clientSocket.OnDisconnected += OnSocketDisconnectEventHandler ;


            //use for server socket
            if(_sessionType == ClientType.System)
            {
                _pingTimer = new System.Threading.Timer(PingCallback, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
            }
        }

        #region  Properties
        public int BytePerPixel => _bytePerPixel;
        public IntPtr Image => _screenRegions.Buffer;
        public bool AcceptScreen => _screenRegions.CanWork;
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
        public string SessionId => _sessionId;
        public ClientType SessionType => _sessionType;
        public ClientSocket Client => _clientSocket;
        public VRegions ScreenRegions => _screenRegions;
        #endregion

        #region Workers
        public int GetStride(int width, int bytePerPixel)
        {
            return _screenRegions.GetStride1(width, bytePerPixel);
        }
        private void PingCallback(object obj)
        {

            var elapsed = (DateTimeOffset.UtcNow - _lastPing).TotalSeconds;

            if (elapsed > TIME_OUT)
            {
                Dispose();
                return;
            }
            //_client.SendPing();
            AddWork(QueuePriority.High, new TaskObject(SocketDataType.Ping, _sessionId, new byte[0], true));
            _lastPing = DateTimeOffset.UtcNow;
        }
        private void SenderWorker(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    bool hasWork = false;
                    if (_highQueue.TryDequeue(out var highTask))
                    {
                        HighQueueHandler(highTask);
                        hasWork = true;
                    }
                    else if (_screenRegions != null && _screenRegions.HasData  && _screenRegions.ReadyToSend())
                    {
                        DirtyRegionSend();
                        hasWork = true;
                    }
                    else if (_lowQueue.TryDequeue(out var lowTask))
                    {
                        LowQueueHandler(lowTask);
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
        private void HighQueueHandler(QueueItem highTask)
        {
            switch (highTask.Data)
            {
                case TaskObject taskObj:
                    if(taskObj.TaskType == SocketDataType.ScreenSend && taskObj.CapturedFrame != null)
                    {
                        Send(taskObj.CapturedFrame);
                        return;
                    }
                    Send(taskObj.TaskType, taskObj.Data, taskObj.SessionId, taskObj.IsSendHeader);
                    break;
                default:
                    break;
            }
        }
        private void LowQueueHandler(QueueItem lowTask)
        {
            var taskObj = lowTask.Data as TaskObject;
            if(taskObj != null)
            {
                if (_cancelFile.Contains(taskObj.ChunkFileInfo.FileId))
                    return;

                ProcessFileTransfer(taskObj);
            }
        }
        private void ReceivedWorker(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {

                try
                {
                    bool hasWork = false;

                    if (_receiveQueue.TryDequeue(out var task))
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
            if(e != null)
            {
                switch (e.Type)
                {
                    case SocketDataType.ScreenOk:
                        ReadyToNextRegionSend();
                        //EnableRegionsSend();
                        OnDataReceived.Invoke(this, new ClientSessionDataReceivedEventArgs(sessionId: this.SessionId, type: e.Type, data: e.Data));
                        break;
                    case SocketDataType.RegionsChangedOk:
                        ReadyToNextRegionSend();
                        break;
                    case SocketDataType.ScreenSend:
                        OnScreenReceived?.Invoke(this, new ClientSessionScreenReceivedEventArgs(SessionManagement.Events.ClientSession.ScreenType.FullScreen, e.Data));
                        break;
                    case SocketDataType.ScreenRegionsChangedSend:
                        OnScreenReceived?.Invoke(this, new ClientSessionScreenReceivedEventArgs(SessionManagement.Events.ClientSession.ScreenType.DirtyRegions, e.Data));
                        break;
                    default:
                        if (OnDataReceived != null)
                            OnDataReceived.Invoke(this, new ClientSessionDataReceivedEventArgs(sessionId: this.SessionId, type: e.Type, data: e.Data));
                        break;
                        
                }
                Interlocked.Exchange(ref _sending, 0);
            }

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
        public void AddWork(QueuePriority priority,TaskObject task)
        {
            if (task == null) return;

            if(priority == QueuePriority.High)
            {
                _highQueue.Enqueue(new QueueItem(task));
            }
            else if(priority  == QueuePriority.Low)
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
        private void Send(CapturedFrame frame)
        {
            if (Interlocked.CompareExchange(ref _disposed, 0, 0) == 1)
                throw new ObjectDisposedException(this.GetType().Name);
            try
            {
                Sendstate state = new Sendstate
                {
                    Data = frame.CompressedData,
                    Remained = frame.CompressedDataLength,
                    Sent = frame.CompressedDataOffset,
                    Timeout = Environment.TickCount,
                    CapturedFrame = frame
                };
                _clientSocket.Send(state);
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "Error when sending data to remote server without specific length");
            }
        }
        public void Send(SocketDataType type, byte[] data, string id = null, bool sendHeader = true)
        {
            if (Interlocked.CompareExchange(ref _disposed, 0, 0) == 1)
                throw new ObjectDisposedException(this.GetType().Name);

            try
            {
                if (type == SocketDataType.None)
                    return;

                if (sendHeader)
                {
                    data = HeaderGenerate(type: type, id: _sessionId, true, data);
                }

                Sendstate state = new Sendstate
                {
                    Data = data,
                    Remained =  data.Length,
                    Sent = 0,
                    Timeout = Environment.TickCount,
                    CapturedFrame = null,
                };

                _clientSocket.Send(state);
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "Error when sending data to remote server without specific length");
            }
        }

        #endregion

        #region ScreenRegion
        private void ScreenToQueue(SocketDataType type, byte[] buffer, int length)
        {
            var header = HeaderGenerate(type: type,
                id: this.SessionId,
                includeData: false,
                data: null,
                dataSize: length);

            var headerPacket = new TaskObject
            {
                TaskType = type,
                Data = header,
                SessionId = this.SessionId,
                IsSendHeader = false
            };
            var payloadPacket = new TaskObject
            {
                TaskType = type,
                SessionId = this.SessionId,
                CapturedFrame = new CapturedFrame(ScreenCapture.Enums.VScreenType.FullScreen, buffer, 0, length, 1),
                IsSendHeader = false
            };
            AddWork(QueuePriority.High, headerPacket, payloadPacket);
        }
        public bool AcceptFullScreen()
        {
            return _screenRegions.AcceptFullScreen();
        }
        public void AddScreen(RegionFrame screen)
        {
            _screenRegions.Add(screen);

            var screenData = _screenRegions.GetData();
            if (screenData == null || screenData.Buffer == null)
                throw new InvalidOperationException("");

            if (!_screenRegions.SetBusy())
                return;

            //Enable nhan dirty regions ngay sau khi xu ly xong full screen, khong doi goi "ScreenOK" moi enable vi the se mat frame
            EnableRegionsSend();
            try
            {
                int length = ScreenCapture.Utils.Compressor.CompressedLZ4(screenData.Buffer, screenData.Length, _bufferPool, _bufferPool.Length);
                var type = SocketDataType.ScreenSend;
                ScreenToQueue(type, _bufferPool, length);
            }
            finally
            {
                VArrayPool.Return(screenData.Buffer);
            }

        }
        private void DirtyRegionSend()
        {
            var dirtyRegions = _screenRegions.GetData();
            if (dirtyRegions == null) return;
            try
            {
                int length = ScreenCapture.Utils.Compressor.CompressedLZ4(dirtyRegions.Buffer, dirtyRegions.Length, _bufferPool, _bufferPool.Length);
                var type = SocketDataType.ScreenRegionsChangedSend;

                var header = HeaderGenerate(type: type,
                   id: this.SessionId,
                   includeData: false,
                   data: null,
                   dataSize: length);

                var frame = new CapturedFrame(ScreenCapture.Enums.VScreenType.DirtyRegions, _bufferPool, 0, length, 1);
                Send(type, header, this.SessionId, false);

                Send(frame);

            }
            catch (Exception ex)
            {
                Console.WriteLine("DirtyRegionSend err: ", ex.Message);
            }
            finally
            {
                VArrayPool.Return(dirtyRegions.Buffer);
            }
        }
        private void EnableRegionsSend()
        {
            _screenRegions.BeginAccept();
        }
        private void ReadyToNextRegionSend()
        {
            _screenRegions.SendCompleted();
        }
        public void AddRegions(RegionFrame frames)
        {
            _screenRegions.Add(frames);
        }
        #endregion

        #region Methods
        public void UpdatePartnerInfo(PartnerNetworkInfo partnerInfo)
        {
            if (partnerInfo == null) throw new ArgumentNullException("partner info");
            _partnerInfo = partnerInfo;
        }
        public byte[] HeaderGenerate(SocketDataType type, string id, bool includeData = false, byte[] data = null, int dataSize = 0, string packetId = null)
        {

            if (type == SocketDataType.None)
                return null;

            if (string.IsNullOrWhiteSpace(id))
                id = this._sessionId;

            try
            {
                int packetIdLength = (string.IsNullOrEmpty(packetId)) ? 0 : packetId.Length;

                int headerOnlySize = RandomLength.DATA_TYPE_LENGTH + ByteConstants.INT32_LENGTH + id.Length;
                int actualDataSize = includeData ? (data.Length + packetIdLength) : (dataSize + packetIdLength);
                int totalMessageSize = headerOnlySize + actualDataSize;
                int headerSize = includeData ? (totalMessageSize + packetIdLength) : (headerOnlySize + packetIdLength);

                byte[] header = new byte[headerSize];
                int offset = 0;

                Buffer.BlockCopy(BitConverter.GetBytes(totalMessageSize), 0, header, offset, ByteConstants.INT32_LENGTH);
                offset += ByteConstants.INT32_LENGTH;

                header[offset] = (byte)type;
                offset += RandomLength.DATA_TYPE_LENGTH;

                byte[] idByteArray = ByteArrayHelper.ConvertStringToByteArray(id, EncodingType.ASCII).GetResult();
                Buffer.BlockCopy(idByteArray, 0, header, offset, idByteArray.Length);
                offset += idByteArray.Length;

                if (packetIdLength != 0)
                {
                    byte[] packetIdByteArray = Encoding.ASCII.GetBytes(packetId);
                    Buffer.BlockCopy(packetIdByteArray, 0, header, offset, packetIdLength);
                    offset += packetIdLength;
                    if (includeData)
                    {
                        Buffer.BlockCopy(data, 0, header, offset, data.Length);
                        offset += data.Length;
                    }
                }
                else
                {
                    if (includeData)
                    {
                        Buffer.BlockCopy(data, 0, header, offset, data.Length);
                        offset += data.Length;
                    }
                }
                return header;
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", GetType().Name).Error(ex, "Generate header error ");
                return null;
            }
        }
        private void ProcessFileTransfer(TaskObject task)
        {
            if (task.ChunkFileInfo == null)
            {
                //Send metadata
                Send(task.TaskType, task.Data, task.SessionId, task.IsSendHeader);
                return;
            }
            //Send file data
            try
            {
                FileHelper.OpenStream(task.ChunkFileInfo.FilePath);
                int headerSize = RandomLength.DATA_TYPE_LENGTH + ByteConstants.INT32_LENGTH + RandomLength.FILE_ID_LENGTH;

                byte[] chunkFileData = new byte[task.ChunkFileInfo.ChunkSize + headerSize];

                if (!Enum.IsDefined(typeof(ChatDataType), (int)task.Data[0]))
                {
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
                int chunkRead = FileHelper.CopyFileDataByOffset(task.ChunkFileInfo.FilePath, task.ChunkFileInfo.Offset, ref chunkFileData, offset, task.ChunkFileInfo.ChunkSize);
                if (chunkRead != chunkFileData.Length - headerSize)
                {
                    RemoveFile(task.ChunkFileInfo.FileId);
                    return;
                }
                Send(task.TaskType, chunkFileData, task.SessionId, task.IsSendHeader);

                if ((task.ChunkFileInfo.Offset + task.ChunkFileInfo.ChunkSize) >= task.ChunkFileInfo.FileLength)
                {
                    bool result = FileHelper.CloseStream(task.ChunkFileInfo.FilePath);
                    if (!result)
                        Logger.Log.ForContext("FileName", this.GetType().Name).Error("Close stream failed");
                }
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "Send chunk file on socket id " + this._sessionId + "error ");
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
                var header = HeaderGenerate(SocketDataType.Disconnect, this._sessionId, true, Encoding.ASCII.GetBytes(_sessionType.ToString()));
                _clientSocket.Send(header);
            }
            catch(Exception ex)
            {
                Logger.Log.ForContext("", "ClientSession").Error(ex, "SendDisconnectedNotification err");
            }
        }
        #endregion

        #region Events
        private void OnSocketConnectedEventHandler(object sender, SocketConnectedEventArgs e)
        {
            if (OnDataReceived != null)
                OnDataReceived.Invoke(this, new ClientSessionDataReceivedEventArgs(sessionId: this.SessionId, SocketDataType.Connect, null, e.Connected));
        }
        private void OnDataReceivedEventHandler(object sender, SocketDataReceivedEventArgs e)
        {
            _receiveQueue.Enqueue(new QueueItem(e));
        }
        private void OnSendCompletedEventHandler(object sender, SocketSendCompletedEventArgs e)
        {
            //Console.WriteLine("Send Completed");
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
                    SendDisconnectedNotification();

                    if (_clientSocket != null)
                        _clientSocket.Dispose();

                    if (_screenRegions != null)
                        _screenRegions.Dispose();

                    if (_cancelationTokenSource != null)
                        _cancelationTokenSource.Cancel();

                    if (_pingTimer != null)
                    {
                        _pingTimer.Change(Timeout.Infinite, Timeout.Infinite);
                        _pingTimer.Dispose();
                    }
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
