using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Layouts;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.VTCPClient;
using static VRemoteDesktop.Utils.DefaultValue;

namespace VRemoteDesktop.Services.FileService
{
    public class FileChunk
    {
        public FileChunk(long offset, long size)
        {
            Offset = offset;
            Size = size;
        }

        public long Offset { get; set; }
        public long Size { get; set; }
    }
    public interface IVFileExtension
    {
        string FilePath { get; }
        bool AddFileInfo(byte[] rawData, bool isSender, out VFileInfo info);
        bool AddFileInfo(FileInfo fileInfo, bool isSender, out VFileInfo info);
        void SendFile(VClient client);
        VFileInfo GetFileSendInfo();
        void UpdateSavePath(string filePath);
        void WriteData(byte[] chunk);
        void Clear();
        void Dispose();
        event EventHandler<FileEventArgs> FileEvent;
    }
    public class VFileExtension : IVFileExtension, IDisposable
    {
        private readonly object _lock= new object();
        private readonly int CHUNK_SIZE = DEFAULT_CHUNK_SIZE;
        private bool _isSender;
        private FileStream _stream;
        private string _fileName;
        private string _filePath;
        private long _fileSize;
        private int _count;
        private DateTime _lastWriteTime;
        private Dictionary<int, FileChunk> _chunksReceived;
        public event EventHandler<FileEventArgs> FileEvent;
        public VFileExtension()
        {
            _isSender = false;
            _count = 0;
            _chunksReceived = new Dictionary<int, FileChunk>();
        }
        public string FilePath => _filePath;
        public bool AddFileInfo(byte[] rawData, bool isSender, out VFileInfo info)
        {
            info = null;

            if (rawData == null || rawData.Length == 0)
                throw new ArgumentNullException(nameof(AddFileInfo));
            
            string[] fileInfoStringArray = Helpers.StringHelper.StringToStringArrayWithSeparator(Encoding.UTF8.GetString(rawData), "|");
            
            if (fileInfoStringArray.Length < 3)
                throw new InvalidOperationException("Missing some data");

            try
            {
                info = new VFileInfo
                {
                    FileExtension = fileInfoStringArray[0],
                    Filename = fileInfoStringArray[1],
                    FileSize = long.Parse(fileInfoStringArray[2])
                };
            }
            catch(Exception ex)
            {
                throw ex;
            }
            if(info != null)
            {
                Add(info, isSender);
                return true;
            }
            else
            {
                return false;
            }

        }
        public bool AddFileInfo(FileInfo fileInfo, bool isSender, out VFileInfo info)
        {
            info = null;

            if (fileInfo == null)
                throw new ArgumentNullException(nameof(AddFileInfo));

            try
            {
                info = new VFileInfo
                {
                    FileExtension = fileInfo.Extension,
                    Filename = fileInfo.Name,
                    FileSize = fileInfo.Length
                };
            }
            catch (Exception ex)
            {
                throw ex;
            }
            if (info != null)
            {
                Add(info, isSender);
                return true;
            }
            else
            {
                return false;
            }

        }
        public VFileInfo GetFileSendInfo()
        {
            string filePath = Helpers.FileHelper.OpenFileDialogAndGetFilePath();
            if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            {
                FileInfo fileInfo = Helpers.FileHelper.GetFileInfo(filePath);
                if(AddFileInfo(fileInfo, true, out VFileInfo info))
                {
                    UpdateSavePath(filePath);
                    return info;
                }
            }
            return null;
        }
        public void UpdateSavePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(UpdateSavePath));

            _filePath = filePath;
            if (!_isSender)
                NewStream(_filePath);
        }
        public void WriteData(byte[] chunk)
        {
            int offset = BitConverter.ToInt32(chunk, 0);
            byte[] data = new byte[chunk.Length - 4];
            Buffer.BlockCopy(chunk, 4, data, 0, data.Length);
            _ = Task.Factory.StartNew(() =>
            {
                WriteDataToFile(offset, data);
            });
        }
        private void WriteDataToFile(int offset, byte[] data)
        {
            try
            {
                if (offset < 0)
                    throw new ArgumentOutOfRangeException(nameof(WriteDataToFile));
                if (data == null || data.Length == 0)
                    throw new ArgumentNullException(nameof(WriteDataToFile));
                if (_stream == null)
                    throw new InvalidOperationException(nameof(WriteDataToFile) + "Must be update file path first");

                lock (_lock)
                {
                    Helpers.FileHelper.WriteToFile(_stream, offset, data);
                }
                if (_chunksReceived != null)
                {
                    _chunksReceived.Add(_count, new FileChunk(offset, data.Length));
                    _count++;

                    long num = _chunksReceived.Sum(x => x.Value.Size);
                    if (num == _fileSize)
                    {
                        FileEvent?.Invoke(this, new FileEventArgs());
                    }
                }
            }
            finally
            {
                _lastWriteTime = DateTime.Now;
            }
        }
        public void SendFile(VClient client)
        {
            try
            {
                _ = Task.Factory.StartNew(() =>
                {
                    BeginSendFile(client);
                });
            }
            catch(Exception ex)
            {
                throw ex;
            }
        }
        private void BeginSendFile(VClient client)
        {
            try
            {
                FileInfo fileInfo = Helpers.FileHelper.GetFileInfo(_filePath);
                long chunkNumber = Helpers.FileHelper.CalculateChunkNumber(fileInfo.Length, CHUNK_SIZE);
                int count = 0;
                while (count < chunkNumber)
                {
                    int offset = count * CHUNK_SIZE;
                    byte[] chunkData = Helpers.FileHelper.GetFileDataByOffset(fileInfo.FullName, offset, CHUNK_SIZE);

                    byte[] dataSend = new byte[chunkData.Length + 4]; //4 byte for offset
                    Buffer.BlockCopy(BitConverter.GetBytes(offset), 0, dataSend, 0, 4);
                    Buffer.BlockCopy(chunkData, 0, dataSend, 4, chunkData.Length);

                    client.AddWork(
                        new TaskObject
                        {
                            TaskType = DataType.FileTransfer,
                            Data = dataSend,
                            SessionId = client.SocketId,
                            IsSendHeader = true,
                            Priority = QueuePriority.Low
                        });
                    count++;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                Clear();
            }
        }
        public void Clear()
        {
            _isSender = false;
            _count = 0;
            _fileName = null;
            _filePath = null;
            _fileSize = 0;
            _chunksReceived = new Dictionary<int, FileChunk>();
            _stream?.Dispose();
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        private void Add(VFileInfo info, bool isSender)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(Add));
            if(string.IsNullOrWhiteSpace(info.Filename))
                throw new ArgumentException(nameof(Add));
            if (info.FileSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(Add));

            _isSender = isSender;
            _fileName = info.Filename;
            _fileSize = info.FileSize;
        }
        private void NewStream(string filePath)
        {
            lock (_lock)
            {
                _stream?.Dispose();
                _stream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write);
            }
        }
        protected virtual void Dispose(bool disposing) 
        {
            if (disposing)
            {
                _stream?.Dispose();
            }
        }  
    }
}
