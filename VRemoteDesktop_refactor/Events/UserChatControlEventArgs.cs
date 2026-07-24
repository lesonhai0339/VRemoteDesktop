using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vsign4.VRemoteDesktop.Enums;
using Vsign4.VRemoteDesktop.Layouts;

namespace Vsign4.VRemoteDesktop.Events
{
    public class UserChatControlEventArgs : EventArgs
    {
        public UserChatControlEventArgs(UserChatControlEventType type, FileAttachmentPanel attachment, string path)
        {
            Type = type;
            Attachment = attachment;
            Path = path;
        }

        public UserChatControlEventType Type { get; set; }
        public FileAttachmentPanel Attachment { get; set; }
        public string Path { get; set; }
    }
}
