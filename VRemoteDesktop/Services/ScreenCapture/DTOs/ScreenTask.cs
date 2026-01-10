using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace VRemoteDesktop.Services.ScreenCapture.DTOs
{
    public class ScreenTask
    {
        public ScreenTask(byte[] buffer)
        {
            Buffer = buffer;
            CompletedEvent = new ManualResetEventSlim(false);
        }
        public byte[] Buffer { get; set; }
        private ManualResetEventSlim CompletedEvent { get; set; }
        public bool Wait(int waitTime)
        {
            return CompletedEvent.Wait(waitTime);
        }
        public void Reset()
        {
            CompletedEvent.Reset();
        }
        public void Complete()
        {
            CompletedEvent.Set();
        }   
    }
}
