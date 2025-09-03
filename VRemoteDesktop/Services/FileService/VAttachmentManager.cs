using Microsoft.SqlServer.Server;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Models;

namespace VRemoteDesktop.Services.FileService
{
    public class VAttachmentManager: IDisposable
    {
        public readonly object _lock= new object();
        public ConcurrentDictionary<string, VFileInfo> _files;
        public VAttachmentManager()
        {
            _files = new ConcurrentDictionary<string, VFileInfo>();
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
            return _files.TryRemove(id, out _);
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
