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
        private static Random rd = new Random();
        private static string digits = "0123456789";
        static byte[] ByteArrayBuilder(Enums.CommandType commandType, string data)
        {
            byte[] dataBytes = Encoding.ASCII.GetBytes(data);
            byte[] byteBuilder = new byte[dataBytes.Length + 1];
            byteBuilder[0] = (byte)commandType;
            Buffer.BlockCopy(dataBytes, 0, byteBuilder, 1, dataBytes.Length);
            return byteBuilder;
        }
        public static string RandomStringNumber(int length)
        {
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < length; i++)
            {
                int index = rd.Next(digits.Length);
                result.Append(digits[index]);
            }
            return result.ToString();
        }
    }
}
