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
        public static int GetMaxOutputLength(int inputLength)
        {
            return LZ4Codec.MaximumOutputLength(inputLength);  
        }
        public static unsafe int CompressedLZ4(byte[] buffer, int bufferLength,  byte[] compressedBuffer, int offset, int compressedBufferLength)
        {
            int compressedLength = LZ4Codec.Encode(
                buffer,
                0,
                bufferLength,
                compressedBuffer,
                offset,
                compressedBufferLength);

            return compressedLength;
        }
        /// <summary>
        /// Compress trên cùng 1 array
        /// </summary>
        /// <param name="data"></param>
        /// <param name="writeOffset"></param>
        /// <returns></returns>
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
