using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Layouts;
using VRemoteDesktop.Services.FileService;

namespace VRemoteDesktop.Events
{

    public class UserChatControlEventArgs: EventArgs
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
