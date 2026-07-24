using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vsign4.VRemoteDesktop.Enums;

namespace Vsign4.VRemoteDesktop.DTOs
{
    public static class BaseResponseExtensions
    {
        public static BaseResponse<T> IsSuccess<T>(this BaseResponse<T> response)
        {
            if (!response.IsSuccess)
                throw response.Exception;
            return response;
        }
        public static T Response<T>(this BaseResponse<T> response)
        {
            return response.Data;
        }
        public static T GetResult<T>(this BaseResponse<T> response)
        {
            if (!response.IsSuccess)
                throw response.Exception;
            return response.Data;
        }
    }
    public class BaseResponse<T>
    {
        public BaseResponse(ResponseStatus status, T data, string message = null, Exception ex = null)
        {
            Status = status;
            Data = data;
            Message = message;
            Exception = ex;
        }

        public ResponseStatus Status { get; set; }
        public T Data { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }

        //helpers
        public bool IsSuccess
        {
            get
            {
                return Status == ResponseStatus.Success;
            }
        }
        public bool HasData
        {
            get
            {
                return Data != null;
            }
        }
        public bool HasMessage
        {
            get
            {
                return !string.IsNullOrEmpty(Message);
            }
        }
        public bool HasException
        {
            get
            {
                return Exception != null;
            }
        }

        public static BaseResponse<T> Success(T data, string message = null)
        {
            return new BaseResponse<T>(ResponseStatus.Success, data, message);
        }
        public static BaseResponse<T> NotFound(string message, Exception ex = null)
        {
            return new BaseResponse<T>(ResponseStatus.NotFound, default(T), message, ex);
        }
        public static BaseResponse<T> Unauthorized(string message, Exception ex = null)
        {
            return new BaseResponse<T>(ResponseStatus.Unauthorized, default(T), message, ex);
        }
        public static BaseResponse<T> Error(string message, Exception ex = null)
        {
            return new BaseResponse<T>(ResponseStatus.Error, default(T), message, ex);
        }
    }
}
