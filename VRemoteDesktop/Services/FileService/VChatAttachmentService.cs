using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.VTCPClient;
using VRemoteDesktop.Utils;
using static VRemoteDesktop.Utils.DefaultValue;


namespace VRemoteDesktop.Services.FileService
{
    public interface IVChatAttachmentService
    {
        event EventHandler<FileEventArgs> FileDataReceivedEvent;
        bool CleanUpFileInfo(string id);
        bool RemoveFileInfo(string id);
        bool ReceivedFileInfo(byte[] rawData, bool isSender, out VFileInfo info);
        bool BuildSenderFileInfo(FileInfo fileInfo, bool isSender, out VFileInfo info);
        VFileInfo GetFileSendInfo();
        void UpdateFileSavePath(string id, string savePath);
        void ProcessFileDataReceived(byte[] rawData);
        List<ChunkFileInfo> CalculateNumberOfChunksFromFileByFileId(string id);
        void Dispose();
    }
    internal class VChatAttachmentService: IVChatAttachmentService, IDisposable
    {
        private volatile bool _disposed = false;    
        private VAttachmentManager _attachmentManager;
        private ConcurrentDictionary<string, FileStream> _curStreams;
        public event EventHandler<FileEventArgs> FileDataReceivedEvent;
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

            if (!CloseFileStream(id))
                return false;

            return _attachmentManager.Remove(id);
        }
        public bool CleanUpFileInfo(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException("Id cannot be null or empty");

            if (!CloseFileStream(id))
                return false;

            return _attachmentManager.CleanUpFile(id);
        }
        public bool ReceivedFileInfo(byte[] rawData, bool isSender, out VFileInfo info)
        {
            info = null;
            if (rawData == null || rawData.Length == 0)
                throw new ArgumentNullException(nameof(rawData));

            string[] fileInfo = Helpers.StringHelper.StringToStringArrayWithSeparator(Encoding.UTF8.GetString(rawData), DEFAULT_SEPARATOR);

            //At least for value (id, filename, fileExtension, file size)
            if (fileInfo.Length < DefaultFileInfo.FILE_INFO_MIN_FIELDS)
                throw new InvalidOperationException("Missing some data");
            //File id
            if (string.IsNullOrWhiteSpace(fileInfo[DefaultFileInfo.FILE_ID_INDEX]))
                throw new ArgumentNullException("File id cannot be null or empty");
            //File name
            if (string.IsNullOrWhiteSpace(fileInfo[DefaultFileInfo.FILE_NAME_INDEX]))
                throw new ArgumentNullException("File name cannot be null or empty");
            //File extension
            if (string.IsNullOrWhiteSpace(fileInfo[DefaultFileInfo.FILE_EXTENSION_INDEX]))
                throw new ArgumentNullException("File extension cannot be null or empty");
            //File size
            if (long.TryParse(fileInfo[DefaultFileInfo.FILE_SIZE_INDEX], out long size))
            {
                if (size <= 0)
                    throw new ArgumentOutOfRangeException("File size cannot equal 0 or negative");
            }
            else
            {
                throw new InvalidDataException("Invalid file size");
            }
            //File checksum
            if (string.IsNullOrWhiteSpace(fileInfo[DefaultFileInfo.FILE_CHECKSUM_INDEX]))
                throw new ArgumentNullException("File checksum cannot be null or empty");

            info = new VFileInfo(
                id: fileInfo[DefaultFileInfo.FILE_ID_INDEX],
                filePath: null,
                filename: fileInfo[DefaultFileInfo.FILE_NAME_INDEX],
                fileExtension: fileInfo[DefaultFileInfo.FILE_EXTENSION_INDEX],
                fileSize: size,
                isSender: isSender,
                checksum: fileInfo[DefaultFileInfo.FILE_CHECKSUM_INDEX]);
            return _attachmentManager.Add(info);
        }
        public bool BuildSenderFileInfo(FileInfo fileInfo, bool isSender, out VFileInfo info)
        {
            info = null;

            if (fileInfo == null)
                throw new ArgumentNullException(nameof(fileInfo));
            // Computing SHA checksum of the whole file.
            // For large files this may take noticeable time due to reading all bytes.
            string checkSum = Helpers.StringHelper.SHAHash(fileInfo.FullName);
            if (string.IsNullOrWhiteSpace(checkSum))
                return false;

            info = new VFileInfo(
                id: null,
                filePath: fileInfo.FullName,
                filename: fileInfo.Name,
                fileExtension: fileInfo.Extension,
                fileSize: fileInfo.Length,
                isSender: isSender,
                checksum: checkSum);
            return _attachmentManager.Add(info);
        }
        public VFileInfo GetFileSendInfo()
        {
            string filePath = Helpers.FileHelper.OpenFileDialogAndGetFilePath();
            if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            {
                FileInfo fileInfo = Helpers.FileHelper.GetFileInfo(filePath);
                if(fileInfo.Length > long.MaxValue)
                    throw new ArgumentOutOfRangeException("File size cannot be larger than " + long.MaxValue);

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
            int headerSize = DefaultFileInfo.OFFSET_INT32_LENGTH + DefaultFileInfo.FILE_ID_LENGTH; //offset length + file id

            if (rawData == null || rawData.Length == 0)
                throw new ArgumentNullException("Data cannot be null or empty");

            //Header will take 20 bytes, 4 byte for offset, 16 byte for file id
            if (rawData.Length < headerSize)
                throw new InvalidOperationException("Data is not valid");
            // Parse header
            int offset = BitConverter.ToInt32(rawData, 0);
            string fileId = Helpers.ByteArrayHelper.ConvertByteArrayToString(rawData, 4, DefaultFileInfo.FILE_ID_LENGTH, EncodingType.ASCII).GetResult();

            //Extract data
            byte[] data = new byte[rawData.Length - headerSize];
            Buffer.BlockCopy(rawData, headerSize, data, 0, data.Length);

            //Get file info and stream
            var fileInfo = _attachmentManager.Get(fileId);
            if (fileInfo == null)
                throw new InvalidOperationException("Does not exist file stream with id: " + fileId);

            var fileStream = FindFileStream(fileId);
            if (fileStream == null)
                throw new InvalidOperationException("Does not exist file stream with id: " + fileId);

            //Update file info
            bool flush = false;
            bool updatedSizeReceived = fileInfo.UpdateReceivedSize(data.Length);
            if (!updatedSizeReceived)
                throw new Exception("Error when update file received");

            bool updatedWriteTime = fileInfo.UpdateWriteTime(DateTime.Now);
            if (!updatedWriteTime)
                throw new Exception("Error when update last write time");

            //Check total data received
            flush = (fileInfo.FileSize == fileInfo.ReceivedSize);
            //Write data to file
            WriteToFile(fileStream, offset, data, flush);
            if (flush)
            {
                if (CloseFileStream(fileId))
                {
                    bool checksumOk = ChecksumCheck(fileInfo);
                    if (!checksumOk)
                    {
                        //Checksum not the same
                        FileDataReceivedEvent?.Invoke(this, new FileEventArgs(FileStatus.CheckSumFailed, fileId, data.Length, fileInfo.SavePath));
                        _attachmentManager.Remove(fileId);
                        return;
                    }
                }
                FileDataReceivedEvent?.Invoke(this, new FileEventArgs(FileStatus.Finished, fileId, data.Length, fileInfo.SavePath));
                _attachmentManager.Remove(fileId);
            }
            else
            {
                //Still not received enough data
                FileDataReceivedEvent?.Invoke(this, new FileEventArgs(FileStatus.NewReceived, fileId, data.Length, fileInfo.SavePath));
            }
        }
        private bool CloseFileStream(string id)
        {
            bool flag = false;

            if (string.IsNullOrWhiteSpace(id))
                return flag;

            if(_curStreams.TryGetValue(id, out FileStream stream))
            {
                try
                {
                    stream.Dispose();
                    _curStreams.TryRemove(id, out _);
                    flag = true;
                }
                catch
                {
                    return flag;
                }
            }
            return flag;
        }
        private bool ChecksumCheck(VFileInfo fileInfo)
        {
            bool flag = false;

            if (fileInfo == null)
                return flag;
            if (string.IsNullOrWhiteSpace(fileInfo.Checksum))
                return flag;
            if (string.IsNullOrWhiteSpace(fileInfo.SavePath))
                return flag;

            string checkSum = Helpers.StringHelper.SHAHash(fileInfo.SavePath);
            flag = checkSum.Equals(fileInfo.Checksum);
            return flag;
        }
        private FileStream FindFileStream(string id)
        {
            return _curStreams.TryGetValue(id, out FileStream stream) ? stream : null;
        }
        private void WriteToFile(FileStream stream, int offset, byte[] data,bool flush)
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (data == null || data.Length == 0)
                throw new ArgumentNullException(nameof(data));
            if (stream == null)
                throw new InvalidOperationException(nameof(stream) + "Must be update file path first");

            Helpers.FileHelper.WriteToFile(stream, offset, data, flush);
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

                byte[] chunkData = new byte[DefaultFileInfo.DEFAULT_CHUNK_FILE_SIZE];
                while (handledSize < fileSize)
                {
                    int offset = handledSize;
                    int bytesRead = Helpers.FileHelper.GetChunkFileDataByOffset(fileInfo.FullName, offset, ref chunkData, DefaultFileInfo.DEFAULT_CHUNK_FILE_SIZE);

                    byte[] dataSend = new byte[bytesRead + 20]; //4 byte for offset + 16 byte for file id
                    Buffer.BlockCopy(BitConverter.GetBytes(offset), 0, dataSend, 0, 4);
                    Buffer.BlockCopy(Encoding.ASCII.GetBytes(fileId), 0, dataSend, 4, fileId.Length);
                    Buffer.BlockCopy(chunkData, 0, dataSend, fileId.Length + 4, bytesRead);

                    client.AddWork(
                        new TaskObject
                        {
                            TaskType = SocketDataType.Chat,
                            Data = dataSend,
                            SessionId = client.SocketId,
                            IsSendHeader = true
                        }, QueuePriority.Low);
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
        /// <param name="id"></param>
        public List<ChunkFileInfo> CalculateNumberOfChunksFromFileByFileId(string id)
        {
            List<ChunkFileInfo> chunks = new List<ChunkFileInfo>();
            //Find VFileInfo
            var info = _attachmentManager.Get(id);
            if (info == null)
                throw new InvalidOperationException("Does not exists file with id: " + id);

            //Get System.IO.VFileInfo
            FileInfo fileInfo = Helpers.FileHelper.GetFileInfo(info.FilePath);
            if (fileInfo == null)
                throw new InvalidOperationException("Does not exists file: " + info.FilePath);

            long fileSize = fileInfo.Length;
            long handledSize = 0;
            //Create chunks file info and calculate chunk size, data offset for each chunk
            while (handledSize < fileSize)
            {
                long offset = handledSize;
                int size = (int)Math.Min(DefaultFileInfo.DEFAULT_CHUNK_FILE_SIZE, fileSize - handledSize);

                chunks.Add(new ChunkFileInfo(fileId: info.Id, filePath: fileInfo.FullName, offset: offset, chunkSize: size));

                handledSize += size;
            }
            return chunks;
        }
        private void FileRemovedEventHandler(object sender, ChatFileRemovedEventArgs e)
        {
            if(!string.IsNullOrWhiteSpace(e.FileId))
            {
                CleanUpFileInfo(e.FileId);
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
                if (_disposed) return;

                if (_attachmentManager != null)
                    _attachmentManager.FileRemoved -= FileRemovedEventHandler;


                foreach (var stream in _curStreams.Values)
                {
                    stream.Dispose();
                }
                _curStreams.Clear();
                _attachmentManager.Dispose();
                _disposed = true;
            }
        }
    }
}
