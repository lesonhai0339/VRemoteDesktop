using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Enums;

namespace VRemoteDesktop.Models
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
        public bool IsSuccess => Status == ResponseStatus.Success;
        public bool HasData => Data != null;
        public bool HasMessage => !string.IsNullOrEmpty(Message);
        public bool HasException => Exception != null;

        public static BaseResponse<T> Success(T data, string message = null)
            => new BaseResponse<T>(ResponseStatus.Success, data, message);
        public static BaseResponse<T> NotFound(string message, Exception ex = null)
            => new BaseResponse<T>(ResponseStatus.NotFound, default, message, ex);
        public static BaseResponse<T> Unauthorized(string message, Exception ex = null)
           => new BaseResponse<T>(ResponseStatus.Unauthorized, default, message, ex);
        public static BaseResponse<T> Error(string message, Exception ex = null)
            => new BaseResponse<T>(ResponseStatus.Error, default, message, ex);
    }
}
