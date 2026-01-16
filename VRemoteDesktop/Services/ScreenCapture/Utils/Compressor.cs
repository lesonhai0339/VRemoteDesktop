using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using LZ4;

namespace VRemoteDesktop.Services.ScreenCapture.Utils
{
    public static class Compressor
    {
        public static unsafe int CompressedLZ4(byte[] data, int writeOffset)
        {
            int startOffset = writeOffset + 1;

            int availableSpace = data.Length - startOffset;

            int compressedLength = LZ4Codec.Encode(
                data, 
                0, 
                writeOffset, 
                data, 
                startOffset, 
                availableSpace);
            
            return compressedLength;    
        }
        public static unsafe int DeCompressedLZ4(byte[] data, int offset, int length,byte[] destination)
        {
            int deCompressedLength = LZ4Codec.Decode(
                data,
                offset,
                length,
                destination,
                0,
                destination.Length);

            return deCompressedLength;
        }
    }
}
