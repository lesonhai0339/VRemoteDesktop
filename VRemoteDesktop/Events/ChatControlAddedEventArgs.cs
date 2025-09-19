using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Models;

namespace VRemoteDesktop.Events
{
    public class ChatControlAddedEventArgs: EventArgs
    {
        public ChatControlAddedEventArgs(ChatControlType type, string connectionId, string content, VFileInfo fileInfo, string connectionName = null)
        {
            Type = type;
            ConnectionId = connectionId;
            Content = content;
            FileInfo = fileInfo;
            ConnectionName = connectionName;
        }
        public ChatControlType Type { get; set; }
        public string ConnectionId { get; set; }    
        public string ConnectionName { get; set; }
        public string Content { get; set; }
        public VFileInfo FileInfo { get; set; }// when type is AttachFile, this property is not null
    }
}
