using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Models
{
    public class DataReceive
    {
        public SocketDataType Type { get; set; }
        public int Length { get; set; }
        public byte[] Data { get; set; }
    }
}
