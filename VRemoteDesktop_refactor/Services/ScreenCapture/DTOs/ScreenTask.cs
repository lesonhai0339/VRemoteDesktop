using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Vsign4.VRemoteDesktop.Services.ScreenCapture.DTOs
{
    public class ScreenTask
    {
        public ScreenTask(byte[] buffer)
        {
            Buffer = buffer;
            DataOffset = 0;
            DataLength = 0;
            CompressedOffset = 0;
            CompressedLength = 0;
            CompletedEvent = new ManualResetEventSlim(false);
        }
        public byte[] Buffer { get; set; }
        public int DataOffset { get; set; }
        public int DataLength { get; set; }
        public int CompressedOffset { get; set; }
        public int CompressedLength { get; set; }
        private ManualResetEventSlim CompletedEvent { get; set; }
        public void Add(int dataOffset, int dataLength, int compressedOffset, int compressLength)
        {
            DataOffset = dataOffset;
            DataLength = dataLength;
            CompressedOffset = compressedOffset;
            CompressedLength = compressLength;
        }
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
            DataOffset = 0;
            DataLength = 0;
            CompressedOffset = 0;
            CompressedLength = 0;
            CompletedEvent.Set();
        }
    }
}
