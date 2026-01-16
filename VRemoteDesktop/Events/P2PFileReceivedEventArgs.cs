using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Enums;

namespace VRemoteDesktop.Events
{
    public class P2PFileReceivedEventArgs : EventArgs
    {
        public P2PFileReceivedEventArgs(ChatFileType type, string filePath)
        {
            this.Type = type;
            this.FilePath = filePath;
        }
        public ChatFileType Type { get; set; }
        public string FilePath { get; set; }
    }
}
