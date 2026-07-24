using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vsign4.VRemoteDesktop.Presenters.DTOs
{
    public enum ChatRespondStatus
    {
        Success,
        Failed,
        Error,
        Timeout
    }
    public class ChatRespond<T>
    {
        public ChatRespond(ChatRespondStatus status) : this(status, string.Empty, string.Empty, default(T)) { }
        public ChatRespond(ChatRespondStatus status, string message) : this(status, message, string.Empty, default(T)) { }
        public ChatRespond(ChatRespondStatus status, string message, string systemMessage) : this(status, message, systemMessage, default(T)) { }
        public ChatRespond(ChatRespondStatus status, string message, string systemMessage, T data)
        {
            Status = status;
            Message = message;
            SystemMessage = systemMessage;
            Data = data;
        }
        public bool IsSuccess
        {
            get
            {
                return Status == ChatRespondStatus.Success;
            }
        }
        public bool HasData
        {
            get
            {
                return Data != null;
            }
        }
        public ChatRespondStatus Status { get; set; }
        public string SystemMessage { get; set; }
        public string Message { get; set; }
        public T Data { get; set; }
    }
}
