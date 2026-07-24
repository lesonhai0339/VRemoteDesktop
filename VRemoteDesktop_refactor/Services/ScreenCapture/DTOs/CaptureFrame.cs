using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vsign4.VRemoteDesktop.Services.ScreenCapture.Enums;

namespace Vsign4.VRemoteDesktop.Services.ScreenCapture.DTOs
{
    public class CapturedFrame
    {
        public VScreenType Type { get; private set; }
        public byte[] CompressedData { get; private set; }
        public int CompressedDataOffset { get; private set; }

        public int CompressedDataLength { get; private set; }


        public CapturedFrame(VScreenType type, byte[] compressedData, int compressedDataOffset, int compressedDataLength)
        {
            Type = type;
            CompressedData = compressedData;
            CompressedDataOffset = compressedDataOffset;
            CompressedDataLength = compressedDataLength;
        }
    }
}
