using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VRemoteServer.RelayServer.Helpers
{
    public static class EncodingHelperExtensions
    {
        public static byte[] StringToByteArray(this Encoding encoding, string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return Array.Empty<byte>();
            return encoding.GetBytes(input);
        }

        public static byte[] StringArrayToByteArrayWithSeparator(this Encoding encoding, char separator, params string[] inputs)
        {
            StringBuilder stringBuilder = new StringBuilder();
            for(int i = 0; i < inputs.Length; i++)
            {
               stringBuilder.Append(inputs[i]).Append(separator);
            }

            return encoding.StringToByteArray(stringBuilder.ToString().TrimEnd(separator));
        }

        public static byte[] StringToByteArray(this Encoding encoding, string input, int index, int length)
        {
            if (string.IsNullOrWhiteSpace(input))
                return Array.Empty<byte>();
            return encoding.GetBytes(input, index, length);
        }

        public static string ByteArrayToString(this Encoding encoding, byte[] input)
        {
            if (input == null || input.Length == 0)
                return string.Empty;
            return encoding.GetString(input);
        }

        public static string ByteArrayToString(this Encoding encoding, byte[] input, int index, int length)
        {
            if (input == null || input.Length == 0)
                return string.Empty;
            return encoding.GetString(input, index , length);
        }

        public static string[] ByteArrayToStringWithSeparator(this Encoding encoding, byte[] input, char separator)
        {
            if (input == null || input.Length == 0)
                return Array.Empty<string>();
            return encoding.GetString(input).Split(separator);
        }

        public static string[] ByteArrayToStringWithSeparator(this Encoding encoding, byte[] input, int index, int length, char separator)
        {
            if (input == null || input.Length == 0)
                return Array.Empty<string>();
            return encoding.GetString(input, index , length).Split(separator);
        }
    }
}
