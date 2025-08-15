using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Events
{
    public class P2PRequestConnectEventArgs : EventArgs
    {
        public P2PRequestConnectEventArgs(bool isSuccess, byte[] data)
        {
            IsSuccess = isSuccess;
            Data = data;
        }

        public bool IsSuccess { get; set; }
        public byte[] Data { get; set; }
    }
}
