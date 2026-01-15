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
        public VScreenSenderEventArgs(VScreenSenderEventType type, byte[] buffer, int dataOffset, int dataLength, int compressedOffset, int compressedLength)
        {
            Type = type;
            Buffer = buffer;
            DataOffset = dataOffset;
            DataLength = dataLength;
            CompressedOffset = compressedOffset;
            CompressedLength = compressedLength;

        }
        public VScreenSenderEventType Type { get; set; }
        public byte[] Buffer { get; set; }
        public int DataOffset { get; set; }

        public int DataLength { get; set; }
        public int CompressedOffset { get; set; }

        public int CompressedLength { get; set; }
    }
}
