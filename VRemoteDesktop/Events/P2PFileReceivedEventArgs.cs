using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Events
{
    public class P2PFileReceivedEventArgs : EventArgs
    {
        public P2PFileReceivedEventArgs(bool acceptSave, string filePath)
        {
            this.AcceptSave = acceptSave;
            this.FilePath = filePath;
        }
        public bool AcceptSave { get; set; }
        public string FilePath { get; set; }
    }
}
