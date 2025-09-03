using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Events
{
    public enum FileStatus
    {
        NewReceived,
        Finished
    }
    public class FileEventArgs: EventArgs
    {
        public FileEventArgs() { }
        public FileEventArgs(FileStatus status, string fileId, int size)
        {
            Status = status;
            FileId = fileId;
            Size = size;
        }
        public FileStatus Status { get; set; }
        public string FileId { get; set; }
        public int Size { get; set; }
    }
}
