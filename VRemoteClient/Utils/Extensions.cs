using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using VRemoteClient.Models.Entities;

namespace VRemoteClient.Utils
{
    public static class Extensions
    {
        private static Random rd = new Random();
        private static string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        private static string digits = "0123456789";
        private static bool bool_0 = false;
        internal static Dictionary<byte, byte> dictionary_0 = new Dictionary<byte, byte>();
        public static void RemoveFirst(ref byte[] data, int cutLength)
        {
            if (cutLength < 0 || cutLength > data.Length)
                throw new ArgumentOutOfRangeException(nameof(cutLength));

            data = data.Skip(cutLength).ToArray();
        }
        public static byte[] Compress(byte[] data)
        {
            if (data == null || data.Length == 0) return data;

            using (MemoryStream stream = new MemoryStream()) 
            {
                using (DeflateStream dstream = new DeflateStream(stream, CompressionMode.Compress, true))
                {
                    dstream.Write(data, 0, data.Length);
                }
                return stream.ToArray();
            }
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
        public static byte[] Decompress(byte[] data)
        {
            MemoryStream input = new MemoryStream(data);
            MemoryStream output = new MemoryStream();
            using (DeflateStream dstream = new DeflateStream(input, CompressionMode.Decompress,true))
            {
                dstream.CopyTo(output);
            }
            return output.ToArray();
        }
        public static byte[] CompressGzip(byte[] data)
        {
            if (data == null || data.Length == 0) return data;

            using (MemoryStream stream = new MemoryStream())
            {
                using (var compressionStream = new GZipStream(stream, CompressionMode.Compress))
                {
                    compressionStream.Write(data, 0, data.Length);
                    compressionStream.Flush();
                }
                return stream.ToArray();
            }
        }

        public static byte[] DecompressGzip(byte[] data)
        {
            using (MemoryStream input = new MemoryStream(data))
            using (MemoryStream output = new MemoryStream())
            using (var compressionStream = new GZipStream(input, CompressionMode.Decompress))
            {
                compressionStream.CopyTo(output);
                return output.ToArray();
            }
        }
        //add padding byte(0x20) to output data = length
        public static byte[] AddPaddingToBytes(byte[] sourceByte, int length = 1025)
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
        public static ClientInfo InitInfo()
        {
            var computerName = Environment.MachineName;
            int width = Screen.PrimaryScreen.Bounds.Width;
            int height = Screen.PrimaryScreen.Bounds.Height;
            OperatingSystem os = Environment.OSVersion;
            ClientInfo info = new ClientInfo
            {
                Id = RandomStringNumber(8),
                Password = RandomStringNumber(4),
                ComputerName = computerName,
                Width = width,
                Height = height,
                MajorVersion = os.Version.Major.ToString(),
                MinorVersion = os.Version.Minor.ToString()
            };
            return info;
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
        public static Bitmap CaptureScreen()
        {
            Rectangle bounds = Screen.PrimaryScreen.Bounds;
            Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            Graphics graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);

            return bitmap;
        }
        public static byte[] BitmapToByteArray(Bitmap bitmap)
        {
            BitmapData bmpdata = null;

            try
            {
                bmpdata = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
                int stride = bmpdata.Stride;
                int numbytes = bmpdata.Stride * bitmap.Height;
                byte[] bytedata = new byte[numbytes];
                IntPtr ptr = bmpdata.Scan0;

                Marshal.Copy(ptr, bytedata, 0, numbytes);

                return bytedata;
            }
            finally
            {
                if (bmpdata != null)
                    bitmap.UnlockBits(bmpdata);
            }
        }
    }
}
