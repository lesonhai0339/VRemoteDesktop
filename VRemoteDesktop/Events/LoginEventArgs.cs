using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Models;

namespace VRemoteDesktop.Events
{
    public class LoginEventArgs: EventArgs
    {
        public LoginEventArgs(bool isSuccess)
        {
            IsSuccess = isSuccess;
        }

        public bool IsSuccess { get; set; }
    }
}
