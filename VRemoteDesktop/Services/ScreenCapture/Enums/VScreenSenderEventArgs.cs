using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Services.ScreenCapture.DTOs;
using VRemoteDesktop.Services.ScreenCapture.GDI;

namespace VRemoteDesktop.Services.ScreenCapture.Enums
{

    public class VScreenSenderEventArgs: EventArgs
    {
        public VScreenSenderEventArgs(VScreenSenderEventType type, byte[] compressedBuffer, int compressedOffset, int compressedLength)
        {
            Type = type;
            CompressedBuffer = compressedBuffer;
            CompressedOffset = compressedOffset;
            CompressedLength = compressedLength;

        }
        public VScreenSenderEventType Type { get; set; }
        public byte[] CompressedBuffer { get; set; }
        public int CompressedOffset { get; set; }

        public int CompressedLength { get; set; }
    }
}
