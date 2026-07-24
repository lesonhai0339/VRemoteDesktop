using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Vsign4.VRemoteDesktop.DTOs;

namespace Vsign4.VRemoteDesktop.Helpers
{
    internal static class StringHelper
    {
        private static Random rd = new Random();
        private static string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        private static string digits = "0123456789";
        internal static Dictionary<byte, byte> dictionary_0 = new Dictionary<byte, byte>();
        public static string StringToStringWithDelimiter(string input, string delimiter, int length)
        {
            List<string> result = new List<string>();
            for (int i = 0; i < input.Length; i += length)
            {
                int num = Math.Min((input.Length - i), 3);
                result.Add(input.Substring(i, num));
            }
            return string.Join(delimiter, result);
        }
        public static string[] StringToStringArrayWithSeparator(string input, string separator = "|")
        {
            if (string.IsNullOrEmpty(input))
                return new string[0];

            return input.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);
        }
        public static string StringBuilderWithSeparator(string separator, params object[] array)
        {
            if (string.IsNullOrWhiteSpace(separator))
                separator = HeaderSchema.Separator;

            if (array == null || array.Length == 0)
                return string.Empty;

            StringBuilder stringBuilder = new StringBuilder();
            int length = array.Length;
            for (int i = 0; i < length; i++)
            {
                if (i > 0)
                    stringBuilder.Append(separator);
                stringBuilder.Append(array[i]);
            }
            return stringBuilder.ToString();
        }
        public static bool StringValidate(string[] inputs)
        {
            return inputs.All(x => StringValidate(x));
        }
        public static bool StringValidate(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }
            return true;
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
                return string.Format("{0} Bytes", size);

            if (size < 1024L * 1024)
                return string.Format("{0:F2} KB", size / 1024f);

            if (size < 1024L * 1024 * 1024)
                return string.Format("{0:F2} MB", size / (1024f * 1024f));

            if (size < 1024L * 1024 * 1024 * 1024)
                return string.Format("{0:F2} GB", size / (1024f * 1024f * 1024f));

            return string.Format("{0:F2} TB", size / (1024f * 1024f * 1024f * 1024f));
        }
        public static string SHAHash(byte[] data)
        {
            using (var sha = SHA1.Create())
            {
                var hash = sha.ComputeHash(data);
                var stringBuilder = new StringBuilder(hash.Length * 2);
                foreach (var item in hash)
                {
                    stringBuilder.Append(item.ToString("X2"));
                }
                return stringBuilder.ToString();
            }
        }
        public static string SHAHash(string filePath)
        {
            if (!File.Exists(filePath))
                return string.Empty;

            using (var sha = SHA1.Create())
            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                var hash = sha.ComputeHash(stream);
                var stringBuilder = new StringBuilder(hash.Length * 2);
                foreach (var item in hash)
                {
                    stringBuilder.Append(item.ToString("X2"));
                }
                return stringBuilder.ToString();
            }
        }
        public static string DataStringBuilder(params object[] data)
        {
            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
            {
                stringBuilder.Append(data[i]);
                if (i != data.Length - 1)
                {
                    stringBuilder.Append(HeaderSchema.Separator);
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
