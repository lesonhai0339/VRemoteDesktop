using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Events
{
    public class FileEventArgs: EventArgs
    {
        public FileEventArgs() { }
        public FileEventArgs(string fileId)
        {
            FileId = fileId;
        }
        public string FileId { get; set; }
    }
}
