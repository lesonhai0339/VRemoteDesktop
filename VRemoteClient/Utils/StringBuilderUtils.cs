using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using VRemoteClient.Models.Entities;

namespace VRemoteClient.Utils
{
    public static class StringBuilderUtils
    {
        private static Random rd = new Random();
        private static string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        private static string digits = "0123456789";
        internal static Dictionary<byte, byte> dictionary_0 = new Dictionary<byte, byte>();
        public static string[] StringToStringArrayWithSeparator(string input, string separator = "|")
        {
            if (string.IsNullOrEmpty(input))
                return new string[0];

            return input.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);
        }
        public static string StringBuilderWithSeparator(string separator = "|", params string[] array)
        {
            if (array == null || array.Length == 0)
                return string.Empty;

            StringBuilder stringBuilder = new StringBuilder();
            int length = array.Length;
            for(int i= 0; i< length; i++)
            {
                if (i > 0)
                    stringBuilder.Append(separator);
                stringBuilder.Append(array[i]);
            }
            return stringBuilder.ToString();
        }
        public static string GenerateStringShortcut(string input, int maxLength = 20)
        {
            if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
                return input;

            return input.Substring(0, maxLength - 3) + "...";
        }
        public static string GetFileSizeString(long size)
        {
            if (size < 1024)
                return $"{size} Bytes";

            if (size < 1024L * 1024)
                return $"{size / 1024f:F2} KB";

            if (size < 1024L * 1024 * 1024)
                return $"{size / (1024f * 1024f):F2} MB";

            if (size < 1024L * 1024 * 1024 * 1024)
                return $"{size / (1024f * 1024f * 1024f):F2} GB";

            return $"{size / (1024f * 1024f * 1024f * 1024f):F2} TB";
        } 
        public static string SHAHash(byte[] data)
        {
            using (var sha= SHA1.Create())
            {
                var hash = sha.ComputeHash(data);
                var stringBuilder = new StringBuilder(hash.Length * 2);
                foreach(var item in hash)
                {
                    stringBuilder.Append(item.ToString("X2"));
                }
                return stringBuilder.ToString();
            }
        }      
        public static string DataStringBuilder(string[] data)
        {
            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
            {
                stringBuilder.Append(data[i]);
                if (i != data.Length - 1)
                {
                    stringBuilder.Append("|");
                }
            }
            return stringBuilder.ToString();
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
        public static string RandomString(int length)
        {
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < length; i++)
            {
                int index = rd.Next(chars.Length);
                result.Append(chars[index]);
            }
            return result.ToString();
        }
    }
}
