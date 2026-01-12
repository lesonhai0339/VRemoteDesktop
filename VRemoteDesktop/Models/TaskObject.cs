using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Services.ScreenCapture.DTOs;

namespace VRemoteDesktop.Models
{
    public class TaskObject
    {
        public TaskObject(): this(SocketDataType.None, null, null, false, null, null) { }

        /// <summary>
        /// Test VScreen
        /// </summary>
        /// <param name="type"></param>
        /// <param name="sessionId"></param>
        /// <param name="data"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        /// <param name="isSendHeader"></param>
        public TaskObject(SocketDataType type, string sessionId, bool isSendHeader, ScreenTask screenTask)
            : this(type, sessionId, null, isSendHeader, screenTask, null) { }

        public TaskObject(SocketDataType type, string sessionId, byte[] data, bool isSendHeader)
           : this(type, sessionId, data, isSendHeader, null, null) { }

        public TaskObject(SocketDataType type, string sessionId, bool isSendHeader, ChunkFileInfo chunkFileInfo)
            : this(type, sessionId, null, isSendHeader, null, chunkFileInfo) { }

        public TaskObject(SocketDataType taskType, string sessionId, byte[] data, bool isSendHeader, ScreenTask screenTask, ChunkFileInfo chunkFileInfo)
        {
            TaskType = taskType;
            SessionId = sessionId;
            Data = data;
            ScreenTask = screenTask;
            IsSendHeader = isSendHeader;
            ChunkFileInfo = chunkFileInfo;
        }

        public SocketDataType TaskType { get; set; } = SocketDataType.None;
        public string SessionId { get; set; }
        public byte[] Data { get; set; } = new byte[0];
        public bool IsSendHeader { get; set; } = true;
        public ScreenTask ScreenTask { get; set; } = null;
        public ChunkFileInfo ChunkFileInfo { get; set; } = null;
    }
}
