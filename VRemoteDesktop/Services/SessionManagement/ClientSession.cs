using LZ4;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
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
using VRemoteDesktop.Services.ScreenCapture.Enums;
using VRemoteDesktop.Services.ScreenCapture.GDI;
using VRemoteDesktop.Services.ScreenCapture.Utils;
using VRemoteDesktop.Services.SessionManagement;
using VRemoteDesktop.Services.SessionManagement.DTOs;
using VRemoteDesktop.Services.SessionManagement.Enums;
using VRemoteDesktop.Services.SessionManagement.Events;
using VRemoteDesktop.Services.SessionManagement.Events.ClientSession;
using VRemoteDesktop.Services.VTCPClient;
using VRemoteDesktop.Services.VTCPClient.Events;
using VRemoteDesktop.Utils;

namespace VRemoteDesktop.Services.RemoteDesktop
{
    public class ClientSession : IDisposable
    {
        private readonly object _lock = new object();
        private const long DELAY_TIME_PER_CHUNK_FILE = 20 * TimeSpan.TicksPerMillisecond;
        private const int TIME_OUT = 30;
        private int _disposed;
        private string _sessionId;
        private ClientType _sessionType;
        private bool _connected;


        private readonly int _width;
        private readonly int _height;
        private readonly int _bytePerPixel;
        private readonly PixelFormat _pixelFormat;

        private int _sending = 0;
        private byte[] _bufferPool;

        private DateTimeOffset _lastPing;

        private long _lastFileSend = Stopwatch.GetTimestamp();

        private PartnerNetworkInfo _partnerInfo;

        private Task _sendTask;
        private Task _receiveTask;
        private System.Threading.Timer _pingTimer;

        private readonly ConcurrentQueue<QueueItem> _highQueue; //Keyboard, mouse, clipboard,...

        private readonly HashSet<string> _cancelFile;
        private readonly ConcurrentQueue<QueueItem> _lowQueue; //File

        private readonly ConcurrentQueue<QueueItem> _receiveQueue;


        private readonly ClientSocket _clientSocket;
        private readonly VRegions _screenRegions;

        private AutoResetEvent _sendWakeUp;

        private CancellationTokenSource _cancelationTokenSource;

        public event EventHandler<ClientSessionDataReceivedEventArgs> OnDataReceived;
        public event EventHandler<ClientSessionDisconnectedEventArgs> OnDisconnected;
        public event EventHandler<ClientSessionDataReceivedEventArgs> OnChatReceived;
        public event EventHandler<ClientSessionScreenReceivedEventArgs> OnScreenReceived;


        //for test
        private Stopwatch _stopwatch = new Stopwatch();
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
            _highQueue = new ConcurrentQueue<QueueItem>();

            _cancelFile = new HashSet<string>();
            _lowQueue = new ConcurrentQueue<QueueItem>();

            _receiveQueue = new ConcurrentQueue<QueueItem>();

            _sendWakeUp = new AutoResetEvent(false);
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
                _bufferPool = VArrayPool.Rent(10 * 1024 * 1024);
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
        public PixelFormat PixelFormat => _pixelFormat; 
        public int BytePerPixel => _bytePerPixel;
        public VBufferSwapper BufferSwapper => _screenRegions.BufferSwapper;
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
        private bool IsDisposed => Interlocked.CompareExchange(ref _disposed, 0, 0) != 0;

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
                    if (_highQueue.Count > 0)
                    {
                        if (HighQueueHandler())
                            hasWork = true;
                    }
                    else if (_screenRegions != null && _screenRegions.HasData  && _screenRegions.ReadyToSend())
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
            if (!_highQueue.TryPeek(out var highTask)) return false;

            var taskObject = highTask.Data as TaskObject;
            if (taskObject == null)
            {
                _highQueue.TryDequeue(out _); 
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

            if (sentSuccessfully)
            {
                _highQueue.TryDequeue(out _);
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

                    if (!_lowQueue.TryPeek(out var queueItem)) return false;

                    var taskObject = queueItem.Data as TaskObject;
                    if (taskObject == null)
                    {
                        _highQueue.TryDequeue(out _);
                        return true; //continue immediately
                    }

                    bool sentSuccessfully = false;

                    if (_cancelFile.Contains(taskObject.ChunkFileInfo.FileId))
                        return true; //ignore because file with this id had been cancelled

                    sentSuccessfully =  ProcessFileTransfer(taskObject);

                    if (sentSuccessfully)
                    {
                        _lowQueue.TryDequeue(out _);
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
                        OnChatReceived?.Invoke(this, new ClientSessionDataReceivedEventArgs(this._sessionId, e.Type, e.Data));
                        break;
                    case SocketDataType.Disconnect:
                    case SocketDataType.RemoteControlDisconnect:
                        Close();
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

            OnScreenReceived?.Invoke(this, new ClientSessionScreenReceivedEventArgs(SessionManagement.Events.ClientSession.ScreenType.FullScreen, e.Data));
        }
        private void DirtyRegionHandler(SocketDataReceivedEventArgs e)
        {
            AddWork(QueuePriority.High,
                new TaskObject(SocketDataType.RegionsChangedOk, this._sessionId, new byte[0], true));

            OnScreenReceived?.Invoke(this, new ClientSessionScreenReceivedEventArgs(SessionManagement.Events.ClientSession.ScreenType.DirtyRegions, e.Data));
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
            if(IsDisposed) return;   

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
                if (data.Length <= 0)
                    data = new byte[0];

                if (sendHeader)
                {
                    //data = HeaderGenerate(type: type, id: _sessionId, true, data);
                    HeaderGenerate(type: type, id: _sessionId, data.Length, _bufferPool, 0);
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
        //public void AddScreen()
        //{
        //    try
        //    {
        //        int compressedLength = GetCaptureFrame(out CapturedFrame frame);
        //        if (frame == null || compressedLength == 0)
        //            return;

        //        var type = SocketDataType.ScreenSend;

        //        var header = HeaderGenerate(type: type,
        //         id: this.SessionId,
        //         includeData: false,
        //         data: null,
        //         dataSize: compressedLength);

        //        var headerPacket = new TaskObject
        //        {
        //            TaskType = type,
        //            Data = header,
        //            SessionId = this.SessionId,
        //            IsSendHeader = false
        //        };
        //        var payloadPacket = new TaskObject
        //        {
        //            TaskType = type,
        //            SessionId = this.SessionId,
        //            CapturedFrame = frame,
        //            IsSendHeader = false
        //        };

        //        AddWork(QueuePriority.High, headerPacket, payloadPacket);
        //    }
        //    catch (Exception ex)
        //    {

        //    }
        //}
        //private void DirtyRegionSend()
        //{
        //    try
        //    {
        //        int compressedLength = GetCaptureFrame(out CapturedFrame frame);
        //        if (frame == null || compressedLength == 0)
        //            return;

        //        var type = SocketDataType.ScreenRegionsChangedSend;
        //        var header = HeaderGenerate(type: type,
        //         id: this.SessionId,
        //         includeData: false,
        //         data: null,
        //         dataSize: compressedLength);


        //        var result1 =  Send(type, header, this._sessionId, false);

        //        var result2 = Send(frame);
        //        if()
        //    }
        //    catch (Exception ex)
        //    {

        //    }
        //}
        public void AddScreen()
        {
            SendCapture(SocketDataType.ScreenSend);
        }
        private bool DirtyRegionSend()
        {
            return SendCapture(SocketDataType.ScreenRegionsChangedSend);
        }
        private bool SendCapture(SocketDataType type)
        {
            var dirtyRegions = _screenRegions.GetData();
            if (dirtyRegions == null)
            {
                _screenRegions.SendCompleted();
                return false;
            }
            try
            {
                //13 bytes for header
                int dataOffset = 13;
                int length = ScreenCapture.Utils.Compressor.CompressedLZ4(dirtyRegions.Buffer, dirtyRegions.Length, _bufferPool, dataOffset, _bufferPool.Length - dataOffset);

                var header = HeaderGenerate(
                    type: type,
                    id: this._sessionId,
                    dataLength: length,
                    buffer: _bufferPool,
                    offset: 0);  

                var frame = new CapturedFrame(ScreenCapture.Enums.VScreenType.DirtyRegions, _bufferPool, 0, length + 13);

                var result =  Send(frame);
                if (result)
                {
                    _screenRegions.ReadCompleted();
                    return true;
                }
                return false;
            }
            finally
            {
                VArrayPool.Return(dirtyRegions.Buffer);
            }
        }
        //private int GetCaptureFrame(out CapturedFrame frame)
        //{
        //    frame = null;
        //    var dirtyRegions = _screenRegions.GetData();
        //    if (dirtyRegions == null)
        //    {
        //        _screenRegions.SendCompleted();
        //        return 0;
        //    }
        //    try
        //    {
        //        int rentLength = Compressor.GetMaxOutputLength(dirtyRegions.Buffer.Length);
        //        var rentBuffer = VArrayPool.Rent(rentLength);

        //        int length = ScreenCapture.Utils.Compressor.CompressedLZ4(dirtyRegions.Buffer, dirtyRegions.Length, rentBuffer, rentBuffer.Length);

        //        frame = new CapturedFrame(ScreenCapture.Enums.VScreenType.DirtyRegions, rentBuffer, 0, length);

        //        return length;
        //    }
        //    finally
        //    {
        //        VArrayPool.Return(dirtyRegions.Buffer);
        //    }
        //}
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
        private int HeaderGenerate(SocketDataType type, string id, int dataLength, byte[] buffer, int offset)
        {
            try
            {
                //1 + 4 + 8 = 13 bytes
                int headerOnlySize = RandomLength.DATA_TYPE_LENGTH + ByteConstants.INT32_LENGTH + id.Length;

                int totalMessageSize = headerOnlySize + dataLength;

                Buffer.BlockCopy(BitConverter.GetBytes(totalMessageSize), 0, buffer, offset, ByteConstants.INT32_LENGTH);
                offset += ByteConstants.INT32_LENGTH;

                buffer[offset] = (byte)type;
                offset += RandomLength.DATA_TYPE_LENGTH;

                byte[] idByteArray = ByteArrayHelper.ConvertStringToByteArray(id, EncodingType.ASCII).GetResult();
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
        //private byte[] HeaderGenerate(SocketDataType type, string id, bool includeData = false, byte[] data = null, int dataSize = 0, string packetId = null)
        //{

        //    if (type == SocketDataType.None)
        //        return null;

        //    if (string.IsNullOrWhiteSpace(id))
        //        id = this._sessionId;

        //    try
        //    {
        //        int packetIdLength = (string.IsNullOrEmpty(packetId)) ? 0 : packetId.Length;

        //        //1 + 4 + 8 = 13 bytes
        //        int headerOnlySize = RandomLength.DATA_TYPE_LENGTH + ByteConstants.INT32_LENGTH + id.Length;


        //        int actualDataSize = includeData ? (data.Length + packetIdLength) : (dataSize + packetIdLength);
        //        int totalMessageSize = headerOnlySize + actualDataSize;
        //        int headerSize = includeData ? (totalMessageSize + packetIdLength) : (headerOnlySize + packetIdLength);

        //        byte[] header = new byte[headerSize];
        //        int offset = 0;

        //        Buffer.BlockCopy(BitConverter.GetBytes(totalMessageSize), 0, header, offset, ByteConstants.INT32_LENGTH);
        //        offset += ByteConstants.INT32_LENGTH;

        //        header[offset] = (byte)type;
        //        offset += RandomLength.DATA_TYPE_LENGTH;

        //        byte[] idByteArray = ByteArrayHelper.ConvertStringToByteArray(id, EncodingType.ASCII).GetResult();
        //        Buffer.BlockCopy(idByteArray, 0, header, offset, idByteArray.Length);
        //        offset += idByteArray.Length;

        //        if (packetIdLength != 0)
        //        {
        //            byte[] packetIdByteArray = Encoding.ASCII.GetBytes(packetId);
        //            Buffer.BlockCopy(packetIdByteArray, 0, header, offset, packetIdLength);
        //            offset += packetIdLength;
        //            if (includeData)
        //            {
        //                Buffer.BlockCopy(data, 0, header, offset, data.Length);
        //                offset += data.Length;
        //            }
        //        }
        //        else
        //        {
        //            if (includeData)
        //            {
        //                Buffer.BlockCopy(data, 0, header, offset, data.Length);
        //                offset += data.Length;
        //            }
        //        }
        //        return header;
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.Log.ForContext("FileName", GetType().Name).Error(ex, "Generate header error ");
        //        return null;
        //    }
        //}
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
                int headerSize = RandomLength.DATA_TYPE_LENGTH + ByteConstants.INT32_LENGTH + RandomLength.FILE_ID_LENGTH;

                byte[] chunkFileData = new byte[task.ChunkFileInfo.ChunkSize + headerSize];

                if (!Enum.IsDefined(typeof(ChatDataType), (int)task.Data[0]))
                {
                    return true; //drop
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
                Send(SocketDataType.Disconnect, null, this._sessionId);
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
        //private void OnSendCompletedEventHandler(object sender, SocketSendCompletedEventArgs e)
        //{
        //    //Console.WriteLine("Send Completed");
        //}
        private void OnSendCompletedEventHandler(object sender, SocketSendCompletedEventArgs e)
        {
            Console.WriteLine("Send Completed");
            Interlocked.Exchange(ref _sending, 0);
            //if (e.State.RentBuffer)
            //{
            //    try
            //    {
            //        //Console.WriteLine($"Return {e.State.Data.Length}\n");
            //        VArrayPool.Return(e.State.Data);
            //    }
            //    catch(Exception ex)
            //    {
            //        Console.WriteLine($"OnSendCompletedEventHandler err: {ex.Message}");
            //    }
            //}
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
                        Task.WaitAll(
                            new[] { _sendTask, _receiveTask }.Where(t => t != null).ToArray(),
                            TimeSpan.FromSeconds(3)
                        );
                    }
                    catch (AggregateException) { }
                    catch (TaskCanceledException) { }

                    _cancelFile.Clear();
                    while (_highQueue.TryDequeue(out _)) { }
                    while (_lowQueue.TryDequeue(out _)) { }
                    while (_receiveQueue.TryDequeue(out _)) { }

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
