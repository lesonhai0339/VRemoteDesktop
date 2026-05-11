using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Presenters.Enums;

namespace VRemoteDesktop.Presenters.DTOs
{
    public class LoginResponse
    {
        public LoginResponse(LoginStatus type, string message = "", object data = null)
        {
            Type = type;
            Message = message;
            Data = data;
        }
    
        public LoginStatus Type { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }
}
