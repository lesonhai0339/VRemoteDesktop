using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Models
{
    public class DataReceive
    {
        public DataReceive(SocketDataType type, int length, byte[] data, string socketId)
        {
            Type = type;
            Length = length;
            Data = data;
            SocketId = socketId;
        }

        public SocketDataType Type { get; set; }
        public int Length { get; set; }
        public byte[] Data { get; set; }
        public string SocketId { get; set; }
    }
}
