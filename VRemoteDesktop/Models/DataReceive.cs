using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Models
{
    public class DataReceive
    {
        public DataType Type { get; set; }
        public string SessionId { get; set; }
        public int Length { get; set; }
        public byte[] Data { get; set; }
    }
}
