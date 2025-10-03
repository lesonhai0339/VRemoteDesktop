using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Layouts;

namespace VRemoteDesktop.Events
{
    public class ChatControlProgressBarUpdateUIEventArgs: EventArgs
    {
        public ChatControlProgressBarUpdateUIEventArgs(string connectionId, string fileId,  int num, FileStatus status)
        {
            ConnectionId = connectionId;    
            FileId = fileId;
            Num = num;
            Status = status;
        }
        public string ConnectionId { get; set; }
        public string FileId { get; set; }
        public FileStatus Status { get; set; }
        public int Num { get; set; }
    }
}
