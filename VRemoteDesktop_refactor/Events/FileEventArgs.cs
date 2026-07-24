using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vsign4.VRemoteDesktop.Services.FileService.Enums;

namespace Vsign4.VRemoteDesktop.Events
{
    public class FileEventArgs : EventArgs
    {
        public FileEventArgs() { }
        public FileEventArgs(string connectionId, FileStatus status, string fileId, int size, string filePath)
        {
            ConnectionId = connectionId;
            Status = status;
            FileId = fileId;
            Size = size;
            FilePath = filePath;
        }
        public string ConnectionId { get; set; }
        public FileStatus Status { get; set; }
        public string FileId { get; set; }
        public int Size { get; set; }
        public string FilePath { get; set; }
    }
}
