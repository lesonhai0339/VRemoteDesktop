using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Events
{
    public class P2PConnectEventArgs: EventArgs
    {
        public P2PConnectEventArgs(bool isSuccess, byte[] data)
        {
            IsSuccess = isSuccess;
            Data = data;
        }

        public bool IsSuccess { get; set; }
        public byte[] Data { get; set; }
    }
}
