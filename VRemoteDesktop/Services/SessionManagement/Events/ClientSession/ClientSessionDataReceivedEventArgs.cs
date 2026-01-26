using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Models;

namespace VRemoteDesktop.Services.SessionManagement.Events.ClientSession
{
    public class ClientSessionDataReceivedEventArgs: EventArgs
    {
        public ClientSessionDataReceivedEventArgs(string sessionId, SocketDataType type, byte[] data, bool isSuccess = true)
        {
            SessionId = sessionId;
            Type = type;
            Data = data;
            IsSuccess = isSuccess;  
        }
    
        public string SessionId { get; set; }
        public SocketDataType Type { get; set; }
        public byte[] Data { get; set; }
        public bool IsSuccess { get; set; }
    }
}
