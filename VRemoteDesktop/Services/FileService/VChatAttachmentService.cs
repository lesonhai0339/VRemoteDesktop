using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.VTCPClient;
using static VRemoteDesktop.Utils.DefaultValue;

namespace VRemoteDesktop.Services.FileService
{
    public interface IVChatAttachmentService
    {
        event EventHandler<FileEventArgs> FileEvent;
        bool RemoveFileInfo(string id);
        bool ReceivedFileInfo(byte[] rawData, bool isSender, out VFileInfo info);
        bool BuildSenderFileInfo(FileInfo fileInfo, bool isSender, out VFileInfo info);
        VFileInfo GetFileSendInfo();
        void UpdateFileSavePath(string id, string savePath);
        void ProcessFileDataReceived(byte[] rawData);
        void SendFile(VClient client, string fileId);
        void Dispose();
    }
    internal class VChatAttachmentService: IVChatAttachmentService, IDisposable
    {
        private volatile bool _disposed = false;    
        private volatile bool _disposing = false;   
        private readonly object _lock = new object();
        private readonly int CHUNK_SIZE = DEFAULT_CHUNK_SIZE;
        private VAttachmentManager _attachmentManager;
        private ConcurrentDictionary<string, FileStream> _curStreams;
        public event EventHandler<FileEventArgs> FileEvent;
        public VChatAttachmentService()
        {
            _attachmentManager = new VAttachmentManager();
            _attachmentManager.FileRemoved += FileRemovedEventHandler;
            _curStreams = new ConcurrentDictionary<string, FileStream>();   
        }
        public bool RemoveFileInfo(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException("Id cannot be null or empty");

            return _attachmentManager.Remove(id);
        }   
        public bool ReceivedFileInfo(byte[] rawData, bool isSender, out VFileInfo info)
        {
            info = null;
            if (rawData == null || rawData.Length == 0)
                throw new ArgumentNullException(nameof(rawData));

            string[] fileInfo = Helpers.StringHelper.StringToStringArrayWithSeparator(Encoding.UTF8.GetString(rawData), "|");

            //At least for value (id, filename, fileExtension, file size)
            if (fileInfo.Length < 4)
                throw new InvalidOperationException("Missing some data");
            //File id
            if (string.IsNullOrWhiteSpace(fileInfo[0]))
                throw new ArgumentNullException("File id cannot be null or empty");
            //File name
            if (string.IsNullOrWhiteSpace(fileInfo[1]))
                throw new ArgumentNullException("File name cannot be null or empty");
            //File extension
            if (string.IsNullOrWhiteSpace(fileInfo[2]))
                throw new ArgumentNullException("File extension cannot be null or empty");
            //File size
            if (long.TryParse(fileInfo[3], out long size))
            {
                if (size <= 0)
                    throw new ArgumentOutOfRangeException("File size cannot equal 0 or negative");
            }
            else
            {
                throw new InvalidDataException("Invalid file size");
            }
            try
            {
                info = new VFileInfo(id: fileInfo[0], filePath: null, filename: fileInfo[1], fileExtension: fileInfo[2], fileSize: size, isSender: false);
                return _attachmentManager.Add(info);
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        public bool BuildSenderFileInfo(FileInfo fileInfo, bool isSender, out VFileInfo info)
        {
            info = null;

            if (fileInfo == null)
                throw new ArgumentNullException(nameof(fileInfo));
            try
            {
                info = new VFileInfo(id: null, filePath: fileInfo.FullName, filename: fileInfo.Name, fileExtension: fileInfo.Extension, fileSize: fileInfo.Length, isSender);
                return _attachmentManager.Add(info);
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        public VFileInfo GetFileSendInfo()
        {
            string filePath = Helpers.FileHelper.OpenFileDialogAndGetFilePath();
            if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            {
                FileInfo fileInfo = Helpers.FileHelper.GetFileInfo(filePath);
                if(fileInfo.Length > int.MaxValue)
                    throw new ArgumentOutOfRangeException("File size cannot be larger than " + int.MaxValue);

                if (BuildSenderFileInfo(fileInfo, true, out VFileInfo info))
                {
                    info.UpdateSavePath(filePath);
                    return info;
                }
            }
            return null;
        }
        public void UpdateFileSavePath(string id, string savePath)
        {
            if(string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException("Id cannot be null or empty");
            if (string.IsNullOrWhiteSpace(savePath))
                throw new ArgumentNullException("Save path cannot be null or empty");

            var fileInfo = _attachmentManager.Get(id);    
            if(fileInfo == null)
                throw new NullReferenceException("Does not exist file info with id: " + id);

            fileInfo.UpdateSavePath(savePath);

            //Create new file stream    
            NewFileStream(id, savePath);    
        }
        private void NewFileStream(string fileId,  string savePath)
        {
            FileStream stream =  Helpers.FileHelper.CreateFileStream(savePath);
            _curStreams.TryAdd(fileId, stream);
        }
        public void ProcessFileDataReceived(byte[] rawData)
        {
            try
            { 
                if (rawData == null || rawData.Length == 0)
                    throw new ArgumentNullException("Data cannot be null or empty");

                //Header will take 20 bytes, 4 byte for offset, 16 byte for file id
                if (rawData.Length < 20)
                    throw new InvalidOperationException("Data is not valid");

                int offset = BitConverter.ToInt32(rawData, 0);
                string fileId = Encoding.ASCII.GetString(rawData, 4, 16);
                byte[] data = new byte[rawData.Length - 20];
                Buffer.BlockCopy(rawData, 20, data, 0, data.Length);

                string filePath = _attachmentManager.Get(fileId)?.FilePath;
                var fileStream = FindFileStream(fileId);
                if (fileStream == null)
                    throw new InvalidOperationException("Does not exist file stream with id: " + fileId);

                bool flush = false;
                //Check and update file info
                var fileInfo = _attachmentManager.Get(fileId);
                if (fileInfo != null)
                {
                    fileInfo.UpdateReceivedSize(data.Length);
                    fileInfo.UpdateWriteTime(DateTime.Now);
                    flush = (fileInfo.FileSize == fileInfo.ReceivedSize);

                    //Write data to file
                    WriteToFile(fileStream, offset, data, flush);
                    if (flush)
                    {
                        //Received enough data
                        FileEvent?.Invoke(this, new FileEventArgs(FileStatus.Finished, fileId, data.Length, filePath));
                        //Remove stream
                        if (_curStreams.TryRemove(fileId, out FileStream stream))
                        {
                            stream?.Dispose();
                        }
                        _attachmentManager.Remove(fileId);
                    }
                    else
                    {
                        //Not received enough data
                        FileEvent?.Invoke(this, new FileEventArgs(FileStatus.NewReceived, fileId, data.Length, filePath));
                    }
                }
            }
            catch(Exception ex)
            {
                throw;
            }   
        }
        private FileStream FindFileStream(string id)
        {
            return _curStreams.TryGetValue(id, out FileStream stream) ? stream : null;
        }
        private void WriteToFile(FileStream stream, int offset, byte[] data,bool flush)
        {
            try
            {
                if (offset < 0)
                    throw new ArgumentOutOfRangeException(nameof(offset));
                if (data == null || data.Length == 0)
                    throw new ArgumentNullException(nameof(data));
                if (stream == null)
                    throw new InvalidOperationException(nameof(stream) + "Must be update file path first");

                Helpers.FileHelper.WriteToFile(stream, offset, data, flush);
            }
            catch(Exception ex)
            {
                throw;
            }
        }
        public void SendFile(VClient client, string fileId)
        {
            try
            {
                _ = Task.Factory.StartNew(() =>
                {
                    BeginSendFile(client, fileId);
                });
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        /// <summary>
        /// Split file data to chunks with header and send to specific client
        /// </summary>
        /// <param name="client">socket connection will be receive file data</param>
        /// <param name="fileId">file id</param>
        [Obsolete("This method require load whole file data to memory")]
        private void BeginSendFileOld(VClient client, string fileId)
        {
            try
            {
                var info = _attachmentManager.Get(fileId);
                if (info == null)
                    throw new InvalidOperationException("Does not exists file with id: " + fileId);

                FileInfo fileInfo = Helpers.FileHelper.GetFileInfo(info.FilePath);
                if (fileInfo == null)
                    throw new InvalidOperationException("Does not exists file: " + info.FilePath);

                int fileSize = (int)fileInfo.Length;
                int handledSize = 0;

                byte[] chunkData = new byte[CHUNK_SIZE];
                while (handledSize < fileSize)
                {
                    int offset = handledSize;
                    int bytesRead = Helpers.FileHelper.GetChunkFileDataByOffset(fileInfo.FullName, offset, ref chunkData, CHUNK_SIZE);

                    byte[] dataSend = new byte[bytesRead + 20]; //4 byte for offset + 16 byte for file id
                    Buffer.BlockCopy(BitConverter.GetBytes(offset), 0, dataSend, 0, 4);
                    Buffer.BlockCopy(Encoding.ASCII.GetBytes(fileId), 0, dataSend, 4, fileId.Length);
                    Buffer.BlockCopy(chunkData, 0, dataSend, fileId.Length + 4, bytesRead);

                    client.AddWork(
                        new TaskObject
                        {
                            TaskType = DataType.FileTransfer,
                            Data = dataSend,
                            SessionId = client.SocketId,
                            IsSendHeader = true,
                            Priority = QueuePriority.Low
                        });
                    //Notify sending progress
                    handledSize += bytesRead;
                }
                chunkData = null;
                //After sending all data, remove file info
                _attachmentManager.Remove(fileId);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        /// <summary>
        /// Split file to chunks and send chunk metadata to specific client(client will take data from file by chunk metadata and send, this will decrease memory usage instead load whole file data to memory)
        /// </summary>
        /// <param name="client"></param>
        /// <param name="fileId"></param>
        private void BeginSendFile(VClient client, string fileId)
        {
            try
            {
                var info = _attachmentManager.Get(fileId);
                if (info == null)
                    throw new InvalidOperationException("Does not exists file with id: " + fileId);

                FileInfo fileInfo = Helpers.FileHelper.GetFileInfo(info.FilePath);
                if (fileInfo == null)
                    throw new InvalidOperationException("Does not exists file: " + info.FilePath);

                int fileSize = (int)fileInfo.Length;
                int handledSize = 0;

                while (handledSize < fileSize)
                {
                    int offset = handledSize;
                    int size = Math.Min(CHUNK_SIZE, fileSize - handledSize);
                    client.AddWork(
                        new TaskObject
                        (
                            type: DataType.FileTransfer,
                            sessionId : client.SocketId,
                            isSendHeader: true,
                            priority: QueuePriority.Low,
                            chunkFileInfo: new ChunkFileInfo(fileId : info.Id, filePath: fileInfo.FullName, offset: offset, chunkSize: size)
                        ));
                    handledSize = offset + size;
                }
                //After sending all data, remove file info
                _attachmentManager.Remove(fileId);
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        private void FileRemovedEventHandler(object sender, ChatFileRemovedEventArgs e)
        {
            if(!string.IsNullOrWhiteSpace(e.FileId))
            {
                _curStreams.TryRemove(e.FileId, out FileStream stream);
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!_disposed)
                {
                    _disposing = true;
                    if (_attachmentManager != null)
                    {
                        _attachmentManager.FileRemoved -= FileRemovedEventHandler;
                        _attachmentManager.Dispose();
                    }
                    foreach (var stream in _curStreams.Values.ToList())
                    {
                        stream?.Dispose();
                    }
                    _disposed = true;
                }
            }
        }
    }
}
