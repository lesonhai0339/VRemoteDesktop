using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vsign4.VRemoteDesktop.Services.FileService.DTOs;
using Vsign4.VRemoteDesktop.Services.ScreenCapture.DTOs;
using Vsign4.VRemoteDesktop.Services.SessionManagement.Enums;

namespace Vsign4.VRemoteDesktop.Services.SessionManagement.DTOs
{
    public class TaskObject
    {

        public TaskObject() : this(taskType: SocketDataType.None, sessionId: null, data: null, isSendHeader: true, capturedFrame: null, chunkFileInfo: null) { }
        public TaskObject(SocketDataType type) : this(taskType: type, sessionId: null, data: null, isSendHeader: true, capturedFrame: null, chunkFileInfo: null) { }
        public TaskObject(SocketDataType type, byte[] data) : this(taskType: type, sessionId: null, data: data, isSendHeader: true, capturedFrame: null, chunkFileInfo: null) { }

        /// <summary>
        /// Test VScreen
        /// </summary>
        /// <param name="type"></param>
        /// <param name="sessionId"></param>
        /// <param name="data"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        /// <param name="isSendHeader"></param>
        public TaskObject(SocketDataType type, string sessionId, bool isSendHeader, CapturedFrame capturedFrame)
            : this(taskType: type, sessionId: sessionId, data: null, isSendHeader: isSendHeader, capturedFrame: capturedFrame, chunkFileInfo: null) { }

        public TaskObject(SocketDataType type, string sessionId, byte[] data, bool isSendHeader)
           : this(taskType: type, sessionId: sessionId, data: data, isSendHeader: isSendHeader, capturedFrame: null, chunkFileInfo: null) { }

        public TaskObject(SocketDataType type, string sessionId, bool isSendHeader, ChunkFileInfo chunkFileInfo)
            : this(taskType: type, sessionId: sessionId, data: null, isSendHeader: isSendHeader, capturedFrame: null, chunkFileInfo: chunkFileInfo) { }


        public TaskObject(SocketDataType taskType, string sessionId, byte[] data, bool isSendHeader, CapturedFrame capturedFrame, ChunkFileInfo chunkFileInfo)
        {
            TaskType = taskType;
            SessionId = sessionId;
            Data = data;
            CapturedFrame = capturedFrame;
            IsSendHeader = isSendHeader;
            ChunkFileInfo = chunkFileInfo;
        }

        public SocketDataType TaskType { get; set; }
        public string SessionId { get; set; }
        public byte[] Data { get; set; }
        public bool IsSendHeader { get; set; }
        public CapturedFrame CapturedFrame { get; set; }
        public ChunkFileInfo ChunkFileInfo { get; set; }
        public int Attempts { get; set; }        =  0;      
    }
}
