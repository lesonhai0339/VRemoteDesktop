using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteClient.Models.DTOs;

namespace VRemoteClient.Utils
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
        public static T GetResult<T>( this BaseResponse<T> response)
        {
            if(!response.IsSuccess)
                throw response.Exception;
            return response.Data;
        }
    }
}
