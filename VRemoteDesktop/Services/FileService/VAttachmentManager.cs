using Microsoft.SqlServer.Server;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using VRemoteDesktop.Events;
using VRemoteDesktop.Models;
using static VRemoteDesktop.Utils.DefaultValue;

namespace VRemoteDesktop.Services.FileService
{
    public class VAttachmentManager: IDisposable
    {
        private readonly object _lock= new object();
        private ConcurrentDictionary<string, VFileInfo> _files;
        private System.Threading.Timer _timer;
        public event EventHandler<ChatFileRemovedEventArgs> FileRemoved;
        public VAttachmentManager()
        {
            _files = new ConcurrentDictionary<string, VFileInfo>();
            _timer = new System.Threading.Timer(CleanupCallback, null, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5));
        }

        private void CleanupCallback(object state)
        {
            if(_files.IsEmpty)
                return;

            var now = DateTime.Now;
            foreach(var file in _files)
            {
                //remove file if last write time exceed over 30 minutes
                if(now -  file.Value.LastWriteTime  > TimeSpan.FromMinutes(DEFAULT_TIMEOUT))
                {
                    FileRemoved?.Invoke(this, new ChatFileRemovedEventArgs(file.Value.Id));
                    _files.TryRemove(file.Key, out _);
                }
            }
        }

        public string New(string fileName, string fileExtension, long fileSize , bool isSender)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException("Filename cannot be null or empty");
            if (string.IsNullOrWhiteSpace(fileExtension))
                throw new ArgumentNullException("FileExtension cannot be null or empty");
            if (fileSize <= 0)
                throw new ArgumentOutOfRangeException("File size cannot equal 0 or negative");
            VFileInfo fileInfo = new VFileInfo(id: null, filePath: null, fileExtension, fileName, fileSize, isSender);
            bool flag = _files.TryAdd(fileInfo.Id, fileInfo);

            if(!flag)
                throw new Exception("Cannot add new file info");

            return fileInfo.Id;
        }
        public bool Add(VFileInfo file)
        {
            if (file == null)
                throw new ArgumentNullException("File cannot be null");
            if(string.IsNullOrWhiteSpace(file.Filename))
                throw new ArgumentNullException("Filename cannot be null or empty");
            if(string.IsNullOrWhiteSpace(file.FileExtension))
                throw new ArgumentNullException("FileExtension cannot be null or empty");
            if(file.FileSize <=0)
                throw new ArgumentOutOfRangeException("FileSize must be greater than zero");

            return _files.TryAdd(file.Id, file);    
        }
        public VFileInfo Get(string id)
        {
            if(string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException("Id cannot be null or empty");

            return _files.TryGetValue(id, out VFileInfo file) ? file : null;
        }
        public bool Remove(string id)
        {
            if(string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException("Id cannot be null or empty");

            if (_files.TryGetValue(id, out var fileinfo))
            {
                if (!string.IsNullOrWhiteSpace(fileinfo.SavePath))
                {
                    bool isSuccess = Helpers.FileHelper.RemoveFileByFilePath(fileinfo.SavePath);
                    if(isSuccess)
                        return false;
                }
                return _files.TryRemove(id, out _);
            }
            return false;
        }
        public bool Update(string id, VFileInfo file)
        {
            if(string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException("Id cannot be null or empty");
            if (file == null)
                throw new ArgumentNullException("File cannot be null");
            if (string.IsNullOrWhiteSpace(file.Filename))
                throw new ArgumentNullException("Filename cannot be null or empty");
            if (string.IsNullOrWhiteSpace(file.FileExtension))
                throw new ArgumentNullException("FileExtension cannot be null or empty");
            if (file.FileSize <= 0)
                throw new ArgumentOutOfRangeException("FileSize must be greater than zero");

            return _files.TryUpdate(id, file, Get(id));
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
                _files.Clear();
            }
        }
    }
}
