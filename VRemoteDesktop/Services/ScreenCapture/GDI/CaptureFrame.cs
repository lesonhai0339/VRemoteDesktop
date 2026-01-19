using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using VRemoteDesktop.Services.ScreenCapture.Enums;

namespace VRemoteDesktop.Services.ScreenCapture.GDI
{
    public class CapturedFrame : IDisposable
    {
        public VScreenSenderEventType Type { get; private set; }
        public byte[] CompressedData { get; private set; }
        public int CompressedDataOffset { get; private set; }

        public int CompressedDataLength { get; private set; }
        public int FrameId { get; private set; }

        public int CurrentRefCount => _refCount;

        private int _refCount;

        public CapturedFrame(VScreenSenderEventType type,  byte[] compressedData, int compressedDataOffset, int compressedDataLength, int count = 0)
        {
            Type = type;
            CompressedData = compressedData;
            CompressedDataOffset = compressedDataOffset;
            CompressedDataLength = compressedDataLength;
            FrameId = Environment.TickCount;
            _refCount = count;
        }
        public void IncRef() { Interlocked.Increment(ref _refCount); }
        public void DecRef()
        {
            if (Interlocked.Decrement(ref _refCount) <= 0)
            {
                ReleaseBuffer();
            }
        }

        public void Dispose()
        {
            DecRef();
        }
        private void ReleaseBuffer()
        {
            if (CompressedData != null)
            {
                VArrayPool.Return(CompressedData);
                CompressedData = null;
            }
        }
    }
}
