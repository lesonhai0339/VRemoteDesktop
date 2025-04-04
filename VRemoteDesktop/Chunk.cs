using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop
{
    public class Chunk
    {
        public Chunk()
        {
            Offset = 0;
            TotalLength = 0;
            Data = null;
        }
        public void Init(int totalLength)
        {
            Offset = 0;
            TotalLength = totalLength;
            Data = new byte[totalLength];
        }
        public bool Add(byte[] newData)
        {
            int num = newData.Length;
            if (Offset + num > TotalLength)
            {
                num = TotalLength - Offset;
            }
            Buffer.BlockCopy(newData, 0, Data, Offset, num);
            Offset += num;
            return IsComplete();
        }
        public void Clear()
        {
            Offset = 0;
            TotalLength = 0;
            Data = null;
        }
        public int GetDataLength()
        {
            return Data.Length;
        }
        public byte[] GetData()
        {
            if (Data == null)
            {
                return new byte[] { };
            }
            return Data;
        }
        public bool IsComplete()
        {
            return Offset >= TotalLength;
        }
        public int TotalLength { get; set; }
        private int Offset { get; set; }
        private byte[] Data { get; set; }
    }
}
