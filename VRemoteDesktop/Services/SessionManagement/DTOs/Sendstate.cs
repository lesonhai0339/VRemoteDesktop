using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Services.ScreenCapture.GDI;

namespace VRemoteDesktop.Models
{
    public class Sendstate
    {
        public int Timeout { get; set; }
        public byte[] Data { get;set; }
        public int Remained { get; set; }
        public int Sent { get; set; }
        public bool RentBuffer { get; set; }
        //public CapturedFrame CapturedFrame { get; set; }
    }
}
