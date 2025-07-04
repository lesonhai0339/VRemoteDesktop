using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRemoteServer.Models;

namespace VRemoteServer.Utils
{
    public static class Extensions
    {
        static byte[] ByteArrayBuilder(Enums.CommandType commandType, string data)
        {
            byte[] dataBytes = Encoding.ASCII.GetBytes(data);
            byte[] byteBuilder = new byte[dataBytes.Length + 1];
            byteBuilder[0] = (byte)commandType;
            Buffer.BlockCopy(dataBytes, 0, byteBuilder, 1, dataBytes.Length);
            return byteBuilder;
        }
    }
}
