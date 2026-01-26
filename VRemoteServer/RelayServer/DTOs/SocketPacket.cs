using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VRemoteServer.RelayServer.DTOs
{
    public  class SocketPacket
    {
        public SocketPacket(byte[] data, int offset, int length)
        {
            Data = data;
            Offset = offset;
            Length = length;
        }
    
        public byte[] Data { get; private set; }
        public int Offset { get; private set; }
        public int Length { get; private set; } 
    }
}
