using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vsign4.VRemoteDesktop.Events
{
    public class ChatFileRemovedEventArgs : EventArgs
    {
        public string FileId { get; private set; }
        public ChatFileRemovedEventArgs(string fileId)
        {
            FileId = fileId;
        }
    }
}
