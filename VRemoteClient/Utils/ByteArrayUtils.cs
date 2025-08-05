using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace VRemoteClient.Utils
{
    public static class ByteArrayUtils
    {
        public static byte[] Combine(byte[] firstArray, byte[] secondArray)
        {
            if (firstArray == null || secondArray == null)
                return new byte[0];

            byte[] combineArray = new byte[firstArray.Length + secondArray.Length];
            Buffer.BlockCopy(firstArray, 0, combineArray, 0, firstArray.Length);
            Buffer.BlockCopy(secondArray, 0, combineArray, firstArray.Length, secondArray.Length);
            return combineArray;
        }
        public static List<byte[]> ByteArrayToListByteArray(byte[] source , int length, int size)
        {
            if(source == null || source.Length == 0 || length == 0 || size == 0)
                return new List<byte[]>();

            List<byte[]> list = new List<byte[]>();

            int numberOfItem = (length + size - 1) / size;
            for (int i = 0; i < numberOfItem; i++)
            {
                int offset = i * size;
                int remain = length - offset;

                int packetSize = Math.Min(remain, size);
                byte[] chunkData = new byte[packetSize];

                Buffer.BlockCopy(source, offset, chunkData, 0, packetSize);
                list.Add(chunkData);
            }

            return list;
        }
    }
}
