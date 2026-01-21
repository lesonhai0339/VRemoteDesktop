using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.ScreenCapture.DTOs;
using VRemoteDesktop.Services.ScreenCapture.GDI;
using VRemoteDesktop.Services.SessionManagement.DTOs;
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
        private VClientType _sessionType;
        private bool _isHost;
        private bool _connected;
        private DateTimeOffset _lastPing;


        private ClientInfo _myInfo;
        private ClientInfo _partnerInfo;


        private System.Threading.Timer _pingTimer;
        private Task _sendTask;
        private Task _receiveTask;

        private readonly ConcurrentQueue<QueueItem> _highQueue; //Keyboard, mouse, clipboard,...
        private readonly ConcurrentQueue<QueueItem> _mediumQueue; //Screen, DirtyRegions

        private readonly HashSet<string> _cancelFile;
        private readonly ConcurrentQueue<ChunkFileInfo> _lowQueue; //File

        private readonly ConcurrentQueue<QueueItem> _receiveQueue;


        private readonly VClient _client;
        private readonly VRegions _screenRegions;

        private AutoResetEvent _sendWakeUp;
        private AutoResetEvent _receivedWakeUp;

        private CancellationTokenSource _cancelationTokenSource;

        public event EventHandler<RemoteDesktopEventArgs> OnDataReceived;
        public event EventHandler<EventArgs> OnDisconnected;
        public ClientSession(string id, VClientType type, bool isHost)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException("id");

            _sessionId = id;
            _sessionType = type;
            _isHost = isHost;
            _highQueue = new ConcurrentQueue<QueueItem>();
            _mediumQueue = new ConcurrentQueue<QueueItem>();


            _cancelFile = new HashSet<string>();
            _lowQueue = new ConcurrentQueue<ChunkFileInfo>();

            _receiveQueue = new ConcurrentQueue<QueueItem>();

            _sendWakeUp = new AutoResetEvent(false);
            _receivedWakeUp = new AutoResetEvent(false);
            _cancelationTokenSource = new CancellationTokenSource();

            _client = new VClient(id, type, isHost);
            _screenRegions = new VRegions(1920, 1080, 3);


            //_pingTimer = new System.Threading.Timer(PingCallback, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
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

            _client.OnDataReceived += OnDataReceivedEventHandler;
        }



        #region  Properties
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
        public ClientInfo MyInfo
        {
            get
            {
                lock (_lock)
                {
                    return _myInfo;
                }
            }
            set
            {
                lock (_lock)
                {
                    _myInfo = value;
                }
            }
        }
        public ClientInfo PartnerInfo
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
        public bool IsHost => _isHost;
        public string SessionId => _sessionId;
        public VClientType SessionType => _sessionType;
        public VClient Client => _client;
        public VRegions ScreenRegions => _screenRegions;
        #endregion

        #region Workers
        private void PingCallback(object obj)
        {

            var elapsed = (DateTimeOffset.UtcNow - _lastPing).TotalSeconds;

            if (elapsed > TIME_OUT)
            {
                Dispose();
                return;
            }
            //_client.SendPing();
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
                    else if (_screenRegions.HasData  && _screenRegions.ReadyToSend())
                    {
                        DirtyRegionSend();
                        hasWork = true;
                    }
                    else if (_lowQueue.TryDequeue(out var lowTask))
                    {
                        if (_cancelFile.Contains(lowTask.FileId)) continue;
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

        private void DirtyRegionSend()
        {
            var dirtyRegions = _screenRegions.GetData();
            if (dirtyRegions == null) return;
            try
            {
                byte[] buffer = VArrayPool.Rent((int)(dirtyRegions.Length * 1.2));
                int length = ScreenCapture.Utils.Compressor.CompressedLZ4(dirtyRegions.Buffer, dirtyRegions.Length, buffer, buffer.Length);
                var type = SocketDataType.ScreenRegionsChangedSend;

                var header = HeaderGenerate(type: type,
                   id: this.SessionId,
                   includeData: false,
                   data: null,
                   dataSize: length);

                var frame = new CapturedFrame(ScreenCapture.Enums.VScreenType.RegionChange, buffer, 0, length, 1);
                Send(type, header, this.SessionId, false);

                _client.SendScreen(frame);

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

        private void HighQueueHandler(QueueItem highTask)
        {
            switch (highTask.Data)
            {
                case TaskObject taskObj:
                    if(taskObj.TaskType == SocketDataType.ScreenSend && taskObj.CapturedFrame != null)
                    {
                        _client.SendScreen(taskObj.CapturedFrame);
                        return;
                    }
                    _client.Send(taskObj.TaskType, taskObj.Data, taskObj.SessionId, taskObj.IsSendHeader);
                    break;
                default:
                    break;
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
                        OnDataReceived.Invoke(this, new RemoteDesktopEventArgs(type: e.Type, data: e.Data));
                        break;
                    case SocketDataType.RegionsChangedOk:
                        ReadyToNextRegionSend();
                        break;
                    default:
                        if (OnDataReceived != null)
                            OnDataReceived.Invoke(this, new RemoteDesktopEventArgs(type: e.Type, data: e.Data));
                        break;
                        
                }
            }
            
        }
        private void EnableRegionsSend()
        {
            Console.WriteLine("Enable regions changed send");
            _screenRegions.BeginAccept();
        }
        private void ReadyToNextRegionSend()
        {
            Console.WriteLine("Finished previous dirty regions send, ready for next");
            _screenRegions.SendCompleted();
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

                _lowQueue.Enqueue(task.ChunkFileInfo);
            }
            //Add queue
            _sendWakeUp.Set();
        }
        public bool TryConnect(string ip, int port, int retry = 0, int timeout = 3000)
        {
            return _client.TryConnect(ip, port, retry, timeout);
        }
        public bool Listen()
        {
            return _client.Listen();
        }
        public void Send(SocketDataType type, byte[] data, string id = null, bool sendHeader = true)
        {
            _client.Send(type, data, id, sendHeader);
        }


        #endregion

        #region ScreenRegion
        public void AddScreen(FullScreenFrame screen)
        {

            try
            {
                byte[] buffer = VArrayPool.Rent((int)(screen.Buffer.Length * 1.2));
                int length = ScreenCapture.Utils.Compressor.CompressedLZ4(screen.Buffer, screen.Length , buffer, buffer.Length);
                var type = SocketDataType.ScreenSend;

                ScreenToQueue(type, buffer, length);
            }
            finally
            {
                screen.DeRef();
            }
        }
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
        public bool FullScreenReceived()
        {
            return _screenRegions.FullScreenReceived();
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
                byte[] buffer = VArrayPool.Rent((int)(screenData.Length * 1.2));
                int length = ScreenCapture.Utils.Compressor.CompressedLZ4(screenData.Buffer, screenData.Length, buffer, buffer.Length);
                var type = SocketDataType.ScreenSend;
                ScreenToQueue(type, buffer, length);
            }
            finally
            {
                VArrayPool.Return(screenData.Buffer);
            }

        }
        public void AddRegions(RegionFrame frames)
        {
            _screenRegions.Add(frames);
        }
        #endregion

        #region Methods
        public void UpdatePartnerInfo(ClientInfo partnerInfo)
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
                //Send(task.TaskType, task.Data, task.SessionId, task.IsSendHeader);
            }
            else
            {
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
                    //Send(task.TaskType, chunkFileData, task.SessionId, task.IsSendHeader);

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
        }
        public void RemoveFile(string fileId)
        {
            _cancelFile.Add(fileId);
        }
        #endregion

        #region Events
        private void OnDataReceivedEventHandler(object sender, SocketDataReceivedEventArgs e)
        {
            _receiveQueue.Enqueue(new QueueItem(e));
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

            Interlocked.Exchange(ref _disposed, 1);

            if (disposing)
            {
                if (_cancelationTokenSource != null)
                    _cancelationTokenSource.Cancel();

                if (_pingTimer != null)
                {
                    _pingTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    _pingTimer.Dispose();
                }

                if (_client != null)
                    _client.Dispose();

                if (_screenRegions != null)
                    _screenRegions.Dispose();
            }
        }
        ~ClientSession()
        {
            Dispose();
        }
    }
}
