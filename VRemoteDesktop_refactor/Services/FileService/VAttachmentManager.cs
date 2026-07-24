using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Vsign4.VRemoteDesktop.DTOs;
using Vsign4.VRemoteDesktop.Events;
using Vsign4.VRemoteDesktop.Services.FileService.DTOs;

namespace Vsign4.VRemoteDesktop.Services.FileService
{
    public class VAttachmentManager : IDisposable
    {
        private readonly object _lock = new object();
        private int _disposed = 0;

        private ConcurrentDictionary<string, VFileInfo> _files;
        private System.Threading.Timer _timer;

        public event EventHandler<ChatFileRemovedEventArgs> FileRemoved;
        public VAttachmentManager()
        {
            _files = new ConcurrentDictionary<string, VFileInfo>();
            _timer = new System.Threading.Timer(CleanupCallback, null, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5));
        }
        //Remove files that are not finished after certain time 
        private void CleanupCallback(object state)
        {
            if (_files.IsEmpty)
                return;

            var now = DateTime.Now;
            foreach (var file in _files)
            {
                //remove file if last write time exceed over 60 minutes
                if (now - file.Value.LastWriteTime > TimeSpan.FromMinutes(HeaderSchema.FileTimeout))
                {
                    if(FileRemoved != null)
                        FileRemoved.Invoke(this, new ChatFileRemovedEventArgs(file.Value.Id));

                    VFileInfo f;
                    _files.TryRemove(file.Key, out f);
                }
            }
        }
        /// <summary>
        /// Add new file 
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public bool Add(VFileInfo file)
        {
            if (file == null)
                throw new ArgumentNullException("File cannot be null");
            if (string.IsNullOrWhiteSpace(file.Filename))
                throw new ArgumentNullException("Filename cannot be null or empty");
            if (string.IsNullOrWhiteSpace(file.FileExtension))
                throw new ArgumentNullException("FileExtension cannot be null or empty");
            if (file.FileSize <= 0)
                throw new ArgumentOutOfRangeException("FileSize must be greater than zero");

            return _files.TryAdd(file.Id, file);
        }
        /// <summary>
        /// Get file info by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public VFileInfo Get(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException("Id cannot be null or empty");
            VFileInfo file;
            return _files.TryGetValue(id, out file) ? file : null;
        }
        /// <summary>
        /// Clear up file info by file id    
        /// </summary>
        /// <param name="fileId"></param>
        /// <returns></returns>
        public bool CleanUpFile(string fileId)
        {
            VFileInfo fileInfo;
            if (_files.TryGetValue(fileId, out fileInfo))
            {
                if (!string.IsNullOrWhiteSpace(fileInfo.SavePath))
                {
                    bool isSuccess = Helpers.FileHelper.RemoveFileByFilePath(fileInfo.SavePath);
                    if (isSuccess)
                        return false;
                }
                return _files.TryRemove(fileId, out fileInfo);
            }
            return false;
        }
        /// <summary>
        /// Remove file existed file info by file id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public bool Remove(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException("Id cannot be null or empty");

            VFileInfo f;
            return _files.TryRemove(id, out f);
        }
        /// <summary>
        /// Update file info    
        /// </summary>
        /// <param name="id"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public bool Update(string id, VFileInfo file)
        {
            if (string.IsNullOrWhiteSpace(id))
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
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;

            if (disposing)
            {
                _files.Clear();

                if(_timer != null)  
                    _timer.Dispose();
            }
        }
    }
}
