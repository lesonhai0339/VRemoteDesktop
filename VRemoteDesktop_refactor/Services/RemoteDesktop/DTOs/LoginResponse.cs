using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vsign4.VRemoteDesktop.Services.RemoteDesktop.DTOs
{
    public class LoginResponse
    {
        public LoginResponse(bool isSuccess, string publicIP)
        {
            IsSuccess = isSuccess;
            PublicIP = publicIP;
        }

        public bool IsSuccess { get; private set; }
        public string PublicIP { get; private set; }
    }
}
