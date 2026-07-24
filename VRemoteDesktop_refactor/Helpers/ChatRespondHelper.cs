using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vsign4.VRemoteDesktop.Presenters.DTOs;

namespace Vsign4.VRemoteDesktop.Helpers
{
    public static class ChatRespondHelper
    {
        public static ChatRespond<T> Failed<T>(string message, string systemMessage)
        {
            return new ChatRespond<T>(ChatRespondStatus.Failed, message, systemMessage, default(T));
        }
        public static ChatRespond<T> Failed<T>(string systemMessage)
        {
            return new ChatRespond<T>(ChatRespondStatus.Failed, string.Empty, systemMessage, default(T));
        }
        public static ChatRespond<T> Error<T>(string message, string systemMessage)
        {
            return new ChatRespond<T>(ChatRespondStatus.Error, message, systemMessage, default(T));
        }
        public static ChatRespond<T> Error<T>(string systemMessage)
        {
            return new ChatRespond<T>(ChatRespondStatus.Error, string.Empty, systemMessage, default(T));
        }
        public static ChatRespond<T> Timeout<T>(string message, string systemMessage)
        {
            return new ChatRespond<T>(ChatRespondStatus.Timeout, message, systemMessage, default(T));
        }
        public static ChatRespond<T> Timeout<T>(string systemMessage)
        {
            return new ChatRespond<T>(ChatRespondStatus.Timeout, string.Empty, systemMessage, default(T));
        }
        public static ChatRespond<T> Success<T>(string message, string systemMessage, T data)
        {
            return new ChatRespond<T>(ChatRespondStatus.Success, message, systemMessage, data);
        }
        public static ChatRespond<T> Success<T>(string message, string systemMessage)
        {
            return new ChatRespond<T>(ChatRespondStatus.Success, message, systemMessage, default(T));
        }
        public static ChatRespond<T> Success<T>(string systemMessage, T data)
        {
            return new ChatRespond<T>(ChatRespondStatus.Success, string.Empty, systemMessage, data);
        }
        public static ChatRespond<T> Success<T>(string systemMessage)
        {
            return new ChatRespond<T>(ChatRespondStatus.Success, string.Empty, systemMessage, default(T));
        }
    }
}
