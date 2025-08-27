using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using VRemoteDesktop.Events;
using VRemoteDesktop.Layouts;

namespace VRemoteDesktop.Services.File
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
        void Add(VFileInfo info, bool isSender);
        void UpdateSavePath(string filePath);
        void WriteDataToFile(int offset, byte[] data);
        event EventHandler<FileEventArgs> FileEvent;
        void Clear();
        void Dispose();
    }
    public class VFileExtension : IVFileExtension, IDisposable
    {
        private readonly object _lock= new object();
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
        public void Add(VFileInfo info, bool isSender)
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
        public void UpdateSavePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(UpdateSavePath));

            _filePath = filePath;
            if(!_isSender)
                NewStream(_filePath);
        }
        public void WriteDataToFile(int offset, byte[] data)
        {
            try
            {
                if (offset < 0)
                    throw new ArgumentOutOfRangeException(nameof(WriteDataToFile));
                if (data == null || data.Length == 0)
                    throw new ArgumentNullException(nameof(WriteDataToFile));
                if(_stream == null)
                    throw new InvalidOperationException(nameof(WriteDataToFile) + "Must be update file path first");

                lock (_lock)
                {
                    Helpers.FileHelper.WriteToFile(_stream, offset, data);
                }
            }
            finally
            {
                _lastWriteTime = DateTime.Now;
                if(_chunksReceived != null)
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
        }
        private void NewStream(string filePath)
        {
            lock (_lock)
            {
                _stream?.Dispose();
                _stream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write);
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
        protected virtual void Dispose(bool disposing) 
        {
            if (disposing)
            {
                _stream?.Dispose();
            }
        }  
    }
}
