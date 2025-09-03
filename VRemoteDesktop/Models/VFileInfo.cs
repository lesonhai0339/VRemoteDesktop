using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Models
{
    public class VFileInfo
    {
        public VFileInfo() { }
        public VFileInfo(string id,string filePath, string fileExtension, string filename, long fileSize, bool isSender)
        {
            Id = id ?? Guid.NewGuid().ToString("N").Substring(0, 16);
            FilePath = filePath ?? string.Empty;
            FileExtension = fileExtension;
            Filename = filename;
            FileSize = fileSize;
            IsSender = isSender;
        }
        public string Id { get; set; }

        public string FileExtension { get; set; }
        public string Filename { get; set; }
        public long FileSize { get; set; }
        public string FilePath { get; set; }
        public string SavePath { get; set; } = string.Empty;
        public long ReceivedSize { get; set; } = 0; 
        public DateTime LastWriteTime { get;set; } = DateTime.MinValue;
        public bool IsSender { get; set; }
        public bool UpdateWriteTime(DateTime newTime)
        {
            if (newTime == null)
                return false;
            LastWriteTime = newTime;
            return true;
        }
        public DateTime GetLastWriteTime()
        {
            return LastWriteTime;
        }
        public bool UpdateReceivedSize(long size)
        {
            if (size <= 0)
                return false;
            ReceivedSize += size;
            return true;
        }
        public long GetCurrentReceivedSize()
        {
            return ReceivedSize;
        }   
        public void UpdateSavePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException("Path cannot be null or empty");
            SavePath = path;
        }
        public string GetSavePath()
        {
            return SavePath;
        }
    }
}
