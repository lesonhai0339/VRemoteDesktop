using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vsign4.VRemoteDesktop.Services.FileService.DTOs
{
    public class VFileInfo
    {
        public VFileInfo() 
        {
            Id = string.Empty;
            FileExtension = string.Empty;
            Filename= string.Empty;
            FileSize = 0;
            FilePath = string.Empty;
            SavePath = string.Empty;    
            ReceivedSize = 0;
            LastWriteTime = DateTime.Now;
            IsSender = false;   
            Checksum = string.Empty;    
        }
        public VFileInfo(string id, string filePath, string filename, string fileExtension, long fileSize, bool isSender, string checksum)
        {
            Id = id ?? Guid.NewGuid().ToString("N").Substring(0, 16);
            FilePath = filePath ?? string.Empty;
            FileExtension = fileExtension;
            Filename = filename;
            FileSize = fileSize;
            IsSender = isSender;
            Checksum = checksum;
            SavePath = string.Empty;
            ReceivedSize = 0;
            LastWriteTime = DateTime.Now;
        }
        public string Id { get; set; }

        public string FileExtension { get; set; }
        public string Filename { get; set; }
        public long FileSize { get; set; }
        public string FilePath { get; set; }
        public string SavePath { get; set; } 
        public long ReceivedSize { get; set; } 
        public DateTime LastWriteTime { get; set; }
        public bool IsSender { get; set; }
        public string Checksum { get; set; }
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
