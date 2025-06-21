using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    internal static class Utils
    {
        internal static byte[] AddPaddingToBytes(byte[] sourceByte, int length = 1024)
        {
            byte[] bytes = new byte[length];
            int byteNeededToAdd = Math.Max(0, length - sourceByte.Length);
            if (sourceByte.Length > length)
            {
                throw new ArgumentException("Data is bigger than buffer size", nameof(sourceByte));
            }
            if (byteNeededToAdd == 0)
            {
                return sourceByte;
            }
            else
            {
                Array.Copy(sourceByte, 0, bytes, 0, sourceByte.Length);
                // can use Array.Fill(bytes, (byte)0x20, sourceByte.Length, byteNeededToAdd); if using .net core  
                for (int i = 0; i < byteNeededToAdd; i++)
                {
                    bytes[sourceByte.Length + i] = 0x20;
                }
            }
            return bytes;
        }
    }
}
