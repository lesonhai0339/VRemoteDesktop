using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Models
{
    public class SckState
    {
        public DateTime Timeout { get; set; }
        public byte[] Data { get;set; }
        public int Remained { get; set; }
        public int Sent { get; set; }
    }
}
