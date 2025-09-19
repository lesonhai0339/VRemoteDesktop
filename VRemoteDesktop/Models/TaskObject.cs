using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Enums;

namespace VRemoteDesktop.Models
{
    public class TaskObject
    {
        public TaskObject()
        {
        } 
        public TaskObject(SocketDataType type, string sessionId, byte[] data, bool isSendHeader)
        {
            TaskType = type;
            SessionId = sessionId;
            Data = data;
            IsSendHeader = isSendHeader;
        }
        public TaskObject(SocketDataType type, string sessionId, bool isSendHeader,ChunkFileInfo chunkFileInfo)
        {
            TaskType = type;
            SessionId = sessionId;
            IsSendHeader = isSendHeader;
            ChunkFileInfo = chunkFileInfo;
        }
        public SocketDataType TaskType { get; set; } = SocketDataType.None;
        public string SessionId { get; set; }
        public byte[] Data { get; set; } = new byte[0];
        public int Length => Data?.Length ?? 0;
        public bool IsSendHeader { get; set; } = true;
        public ChunkFileInfo ChunkFileInfo { get; set; } = null;
    }
}
