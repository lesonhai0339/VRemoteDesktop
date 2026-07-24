using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Vsign4.VRemoteDesktop.DTOs;
using Vsign4.VRemoteDesktop.Enums;
using Vsign4.VRemoteDesktop.Events;
using Vsign4.VRemoteDesktop.Services.FileService.DTOs;
using Vsign4.VRemoteDesktop.Services.FileService.Enums;

namespace Vsign4.VRemoteDesktop.Services.FileService
{
    public interface IVChatAttachmentService
    {
        event EventHandler<FileEventArgs> FileDataReceivedEvent;
        /// <summary>
        /// Xóa file khỏi collection(hoàn thành hoặc thất bại) và cả trên máy( trường hợp cancel file hoặc timeout với file đã gửi dữ liệu)
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        bool CleanUpFileInfo(string id);
        /// <summary>
        /// Xóa file info khỏi collection( khi từ chối file)
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        bool RemoveFileInfo(string id);
        /// <summary>
        /// Lấy thông tin file được gửi đến từ đối tác 
        /// </summary>
        /// <param name="rawData"></param>
        /// <param name="isSender"></param>
        /// <param name="info"></param>
        /// <returns></returns>
        bool ReceivedFileInfo(byte[] rawData, bool isSender, out VFileInfo info);
        /// <summary>
        /// Khởi tạo đối tượng chứa các trường thông tin của file sẽ được gửi đến đối tác
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <param name="isSender"></param>
        /// <param name="info"></param>
        /// <returns></returns>
        bool BuildSenderFileInfo(FileInfo fileInfo, bool isSender, out VFileInfo info);
        /// <summary>
        /// Lấy thông tin file, hiển thị hộp thoại chọn file
        /// </summary>
        /// <returns></returns>
        VFileInfo GetFileSendInfo();
        /// <summary>
        /// Lấy thông tin file thông qua đường dẫn file
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        VFileInfo GetFileSendInfo(string filePath);
        /// <summary>
        /// Cập nhật đường dẫn lưu file
        /// </summary>
        /// <param name="id"></param>
        /// <param name="savePath"></param>
        void UpdateFileSavePath(string id, string savePath);
        /// <summary>
        /// Xử lý dữ liệu file nhận được từ đối tác. Dữ liệu được gửi theo dạng chunk kèm theo offset và length của chunk
        /// <para>1. Tách metadata</para>
        /// <para>2. Ghi data vào offset với length tương ứng trong metadata</para>
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="rawData"></param>
        void ProcessFileDataReceived(string connectionId, byte[] rawData);
        /// <summary>
        /// Tính toàn số lượng chunk, offset, size của từng chunk từ file
        /// </summary>
        /// <param name="id"></param>
        /// <returns>Danh sách chứa tất cả chunks của file. Xem <see cref="ChunkFileInfo"/></returns>
        List<ChunkFileInfo> CalculateNumberOfChunksFromFileByFileId(string id);
        void Dispose();
    }
    internal class VChatAttachmentService : IVChatAttachmentService, IDisposable
    {
        private int _disposed = 0;

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
                throw new ArgumentNullException("raw Data");

            string[] fileInfo = Helpers.StringHelper.StringToStringArrayWithSeparator(Encoding.UTF8.GetString(rawData), HeaderSchema.Separator);

            //At least for value (id, filename, fileExtension, file size)
            if (fileInfo.Length < HeaderSchema.FileFieldCount)
                throw new InvalidOperationException("Missing some data");
            //File id
            if (string.IsNullOrWhiteSpace(fileInfo[HeaderSchema.FileIdIndex]))
                throw new ArgumentNullException("File id cannot be null or empty");
            //File name
            if (string.IsNullOrWhiteSpace(fileInfo[HeaderSchema.FileNameIndex]))
                throw new ArgumentNullException("File name cannot be null or empty");
            //File extension
            if (string.IsNullOrWhiteSpace(fileInfo[HeaderSchema.FileExtensionIndex]))
                throw new ArgumentNullException("File extension cannot be null or empty");
            //File size
            long size = 0;  
            if (long.TryParse(fileInfo[HeaderSchema.FileSizeIndex], out size))
            {
                if (size <= 0)
                    throw new ArgumentOutOfRangeException("File size cannot equal 0 or negative");
            }
            else
            {
                throw new InvalidDataException("Invalid file size");
            }
            //File checksum
            if (string.IsNullOrWhiteSpace(fileInfo[HeaderSchema.FileCheckSumIndex]))
                throw new ArgumentNullException("File checksum cannot be null or empty");

            info = new VFileInfo(
                id: fileInfo[HeaderSchema.FileIdIndex],
                filePath: null,
                filename: fileInfo[HeaderSchema.FileNameIndex],
                fileExtension: fileInfo[HeaderSchema.FileExtensionIndex],
                fileSize: size,
                isSender: isSender,
                checksum: fileInfo[HeaderSchema.FileCheckSumIndex]);
            return _attachmentManager.Add(info);
        }
        public bool BuildSenderFileInfo(FileInfo fileInfo, bool isSender, out VFileInfo info)
        {
            info = null;

            if (fileInfo == null)
                throw new ArgumentNullException("fileInfo");
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
            return GetFileInfo(filePath);
        }
        public VFileInfo GetFileSendInfo(string filePath)
        {
            return GetFileInfo(filePath);
        }
        private VFileInfo GetFileInfo(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException("File path is not null or empty");

            if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            {
                FileInfo fileInfo = Helpers.FileHelper.GetFileInfo(filePath);
                if (fileInfo.Length > long.MaxValue)
                    throw new ArgumentOutOfRangeException("File size cannot be larger than " + long.MaxValue);

                VFileInfo info;
                if (BuildSenderFileInfo(fileInfo, true, out info))
                {
                    info.UpdateSavePath(filePath);
                    return info;
                }
            }
            return null;
        }
        public void UpdateFileSavePath(string id, string savePath)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException("Id cannot be null or empty");
            if (string.IsNullOrWhiteSpace(savePath))
                throw new ArgumentNullException("Save path cannot be null or empty");

            var fileInfo = _attachmentManager.Get(id);
            if (fileInfo == null)
                throw new NullReferenceException("Does not exist file info with id: " + id);

            fileInfo.UpdateSavePath(savePath);

            //Create new file stream    
            NewFileStream(id, savePath);
        }
        private void NewFileStream(string fileId, string savePath)
        {
            FileStream stream = Helpers.FileHelper.CreateFileStream(savePath);
            _curStreams.TryAdd(fileId, stream);
        }
        //Important, error in this methods will close app immediately
        public void ProcessFileDataReceived(string connectionId, byte[] rawData)
        {
            int headerSize = HeaderSchema.PayloadBytes + HeaderSchema.FileIdBytes; //offset length + file id

            if (rawData == null || rawData.Length == 0)
                throw new ArgumentNullException("Data cannot be null or empty");

            //Header will take 20 bytes, 4 byte for offset, 16 byte for file id
            if (rawData.Length < headerSize)
                throw new InvalidOperationException("Data is not valid");
            // Parse header
            int offset = BitConverter.ToInt32(rawData, 0);
            string fileId = Helpers.ByteArrayHelper.ConvertByteArrayToString(rawData, 4, HeaderSchema.FileIdBytes, EncodingType.ASCII).GetResult();

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
                        if(FileDataReceivedEvent != null)
                            FileDataReceivedEvent.Invoke(this, new FileEventArgs(connectionId, FileStatus.CheckSumFailed, fileId, data.Length, fileInfo.SavePath));
                        _attachmentManager.Remove(fileId);
                        return;
                    }
                }
                if (FileDataReceivedEvent != null)
                    FileDataReceivedEvent.Invoke(this, new FileEventArgs(connectionId, FileStatus.Finished, fileId, data.Length, fileInfo.SavePath));
                _attachmentManager.Remove(fileId);
            }
            else
            {
                //Still not received enough data
                if (FileDataReceivedEvent != null)
                    FileDataReceivedEvent.Invoke(this, new FileEventArgs(connectionId, FileStatus.NewReceived, fileId, data.Length, fileInfo.SavePath));
            }
        }
        private bool CloseFileStream(string id)
        {
            bool flag = false;

            if (string.IsNullOrWhiteSpace(id))
                return flag;
            FileStream stream;
            if (_curStreams.TryGetValue(id, out stream))
            {
                try
                {
                    stream.Dispose();
                    _curStreams.TryRemove(id, out stream);
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
            FileStream stream;
            return _curStreams.TryGetValue(id, out stream) ? stream : null;
        }
        private void WriteToFile(FileStream stream, int offset, byte[] data, bool flush)
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset");
            if (data == null || data.Length == 0)
                throw new ArgumentNullException("data");
            if (stream == null)
                throw new InvalidOperationException("Must be update file path first");

            Helpers.FileHelper.WriteToFile(stream, offset, data, flush);
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
                int size = (int)Math.Min(HeaderSchema.FileChunkSize, fileSize - handledSize);

                chunks.Add(new ChunkFileInfo(fileId: info.Id, filePath: fileInfo.FullName, fileLength: fileInfo.Length, offset: offset, chunkSize: size));

                handledSize += size;
            }
            return chunks;
        }
        private void FileRemovedEventHandler(object sender, ChatFileRemovedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.FileId))
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
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;

            if (disposing)
            {
                if (_attachmentManager != null)
                    _attachmentManager.FileRemoved -= FileRemovedEventHandler;


                foreach (var stream in _curStreams.Values)
                {
                    stream.Dispose();
                }
                _curStreams.Clear();
                _attachmentManager.Dispose();
            }
        }
    }
}
