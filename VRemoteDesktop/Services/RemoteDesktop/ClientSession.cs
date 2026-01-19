using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.ScreenCapture.DTOs;
using VRemoteDesktop.Services.ScreenCapture.GDI;
using VRemoteDesktop.Services.VTCPClient;
using VRemoteDesktop.Utils;

namespace VRemoteDesktop.Services.RemoteDesktop
{
    public class ClientSession: IDisposable
    {
        private const int TIME_OUT = 30;
        private int _disposed;
        private string _sessionId;
        private DateTimeOffset _lastPing;


        private System.Threading.Timer _pingTimer;
        private Task _sendTask;
        private Task _receiveTask;

        private readonly ConcurrentQueue<object> _highQueue; //Keyboard, mouse, clipboard,...
        private readonly ConcurrentQueue<object> _mediumQueue; //Screen, DirtyRegions

        private readonly HashSet<string> _cancelFile;
        private readonly ConcurrentQueue<ChunkFileInfo> _lowQueue; //File

        private readonly ConcurrentQueue<object> _receiveQueue; 


        private readonly VClient _client;
        private readonly VRegions _screenRegions;

        private AutoResetEvent _sendWakeUp;
        private AutoResetEvent _receivedWakeUp;

        private CancellationTokenSource _cancelationTokenSource;
        public ClientSession(string id, VClientType type, bool isHost)
        {
            if(string.IsNullOrEmpty(id)) throw new ArgumentNullException("id");

            _sessionId = id;
            _highQueue = new ConcurrentQueue<object>(); 
            _mediumQueue = new ConcurrentQueue<object>();


            _cancelFile = new HashSet<string>();    
            _lowQueue = new ConcurrentQueue<ChunkFileInfo>();

            _receiveQueue = new ConcurrentQueue<object>();

            _sendWakeUp = new AutoResetEvent(false);
            _receivedWakeUp = new AutoResetEvent(false);
            _cancelationTokenSource = new CancellationTokenSource();    

            _client = new VClient(id, type, isHost);
            _screenRegions = new VRegions();


            _pingTimer = new System.Threading.Timer(PingCallback, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
            _sendTask = new Task(() => SenderWorker(_cancelationTokenSource.Token), _cancelationTokenSource.Token);
            _receiveTask = new Task(() => ReceivedWorker(_cancelationTokenSource.Token), _cancelationTokenSource.Token);
        }



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
                        hasWork = true;
                    }
                    else if (_mediumQueue.TryDequeue(out var mediumTask))
                    {
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
        private void ReceivedWorker(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    bool hasWork = false;

                    if (_receiveQueue.TryDequeue(out var task))
                    {
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


        #endregion

        #region VClient
        public void Addwork()
        {
            //Add queue
            _sendWakeUp.Set();  
        }
        public bool TryConnect(string id, int port, int retry = 0, int timeout = 3000)
        {
           return _client.TryConnect(id, port, retry, timeout);    
        }
        public bool Listen()
        {
            return _client.Listen();    
        }


        #endregion

        #region ScreenRegion
        public void AddScreen(long order, RegionFrame frame)
        {
            _screenRegions.Add(order, frame);
        }

        #endregion

        #region Methods
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
                if(_cancelationTokenSource != null)
                    _cancelationTokenSource.Cancel();

                if(_pingTimer != null)
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
