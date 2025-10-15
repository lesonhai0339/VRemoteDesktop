using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using VRemoteDesktop.Models;
using VRemoteDesktop.Enums;

namespace VRemoteDesktop.Helpers
{
     /// <summary>
     /// Chua cac ham xu ly byte array
     /// </summary>
    internal static class ByteArrayHelper
    {

        public static BaseResponse<string> ConvertByteArrayToString(byte[] data, EncodingType encoding)
        {
            if (data == null || data.Length == 0)
                return BaseResponse<string>.Error(
                  message: nameof(ConvertStringToByteArray),
                  ex: new ArgumentException("Data cannot be null or empty")
                );

            Encoding encoder = (encoding == EncodingType.ASCII) ? Encoding.ASCII :
                (encoding == EncodingType.UTF8) ? Encoding.UTF8 :
                null;

            if (encoder != null)
            {
                return BaseResponse<string>.Success(
                    data: encoder.GetString(data),
                    message: nameof(ConvertStringToByteArray)
                );
            }

            return BaseResponse<string>.Error(
                   message: nameof(ConvertStringToByteArray),
                   ex: new ArgumentException("Unexpected encoding type")
            );
        }
        public static BaseResponse<string> ConvertByteArrayToString(byte[] data,int offset,int length, EncodingType encoding)
        {
            if (data == null || data.Length == 0)
                return BaseResponse<string>.Error(
                  message: nameof(ConvertStringToByteArray),
                  ex: new ArgumentException("Data cannot be null or empty")
                );

            Encoding encoder = (encoding == EncodingType.ASCII) ? Encoding.ASCII :
                (encoding == EncodingType.UTF8) ? Encoding.UTF8 :
                null;

            if (encoder != null)
            {
                return BaseResponse<string>.Success(
                    data: encoder.GetString(data, offset, length),
                    message: nameof(ConvertStringToByteArray)
                );
            }

            return BaseResponse<string>.Error(
                   message: nameof(ConvertStringToByteArray),
                   ex: new ArgumentException("Unexpected encoding type")
            );
        }
        public static BaseResponse<byte[]> ConvertStringToByteArray(string data, EncodingType encoding)
        {
            if (string.IsNullOrWhiteSpace(data))
                return BaseResponse<byte[]>.Error(
                  message: nameof(ConvertStringToByteArray),
                  ex: new ArgumentException("Data cannot be null or empty")
                );

            Encoding encoder = (encoding == EncodingType.ASCII) ? Encoding.ASCII :
                (encoding == EncodingType.UTF8) ? Encoding.UTF8 :
                null;

            if (encoder != null)
            {
                return BaseResponse<byte[]>.Success(
                    data: encoder.GetBytes(data),
                    message: nameof(ConvertStringToByteArray)
                );
            }

            return BaseResponse<byte[]>.Error(
                   message: nameof(ConvertStringToByteArray),
                   ex: new ArgumentException("Unexpected encoding type")
            );
        }
        public static BaseResponse<byte[]> ConvertStringToByteArray(string data,int offset, int length, EncodingType encoding)
        {
            if (string.IsNullOrWhiteSpace(data))
                return BaseResponse<byte[]>.Error(
                  message: nameof(ConvertStringToByteArray),
                  ex: new ArgumentException("Data cannot be null or empty")
                );

            Encoding encoder = (encoding == EncodingType.ASCII) ? Encoding.ASCII :
                (encoding == EncodingType.UTF8) ? Encoding.UTF8 :
                null;

            if (encoder != null)
            {
                return BaseResponse<byte[]>.Success(
                    data: encoder.GetBytes(data.ToCharArray(), offset, length),
                    message: nameof(ConvertStringToByteArray)
                );
            }

            return BaseResponse<byte[]>.Error(
                   message: nameof(ConvertStringToByteArray),
                   ex: new ArgumentException("Unexpected encoding type")
            );
        }
        public static BaseResponse<byte[]> BitmapToByteArray(Bitmap bitmap)
        {
            BitmapData bmpData = null;

            try
            {
                bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
                int stride = bmpData.Stride;
                int numBytes = bmpData.Stride * bitmap.Height;
                byte[] byteData = new byte[numBytes];
                IntPtr ptr = bmpData.Scan0;

                Marshal.Copy(ptr, byteData, 0, numBytes);

                return BaseResponse<byte[]>.Success(
                       data: byteData,
                       message: nameof(BitmapToByteArray)
                   );
            }
            catch (Exception ex)
            {
                return BaseResponse<byte[]>.Error(
                    message: nameof(BitmapToByteArray),
                    ex: ex
                );
            }
            finally
            {
                if (bmpData != null)
                    bitmap.UnlockBits(bmpData);
            }
        }
        /// <summary>
        /// Cat bo cutLength byte tu dau mang va thay doi kich thuoc mang
        /// </summary>
        /// <param name="data"></param>
        /// <param name="cutLength"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static BaseResponse<byte[]> RemoveFirstInSource(byte[] data, int cutLength)
        {
            if (cutLength < 0 || cutLength > data.Length)
                throw new ArgumentOutOfRangeException(nameof(cutLength));
            try
            {
                Array.Copy(data, cutLength, data, 0, data.Length - cutLength);
                Array.Resize(ref data, data.Length - cutLength);
                return BaseResponse<byte[]>.Success(
                       data: data,
                       message: nameof(RemoveFirstInSource)
                   );
            }
            catch (Exception ex)
            {
                return BaseResponse<byte[]>.Error(
                    message: nameof(RemoveFirstInSource),
                    ex: ex
                );
            }
        }
        /// <summary>
        /// Cat bo cutLength byte tu dau va tra ve mang moi chua du lieu con lai
        /// </summary>
        /// <param name="data"></param>
        /// <param name="cutLength"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static BaseResponse<byte[]> RemoveFirstNew(byte[] data, int cutLength)
        {
            if (cutLength < 0 || cutLength > data.Length)
                throw new ArgumentOutOfRangeException(nameof(cutLength));
            try
            {
                byte[] newArray = new byte[data.Length - cutLength];

                Buffer.BlockCopy(data, cutLength, newArray, 0, newArray.Length);
                return BaseResponse<byte[]>.Success(
                       data: newArray,
                       message: nameof(RemoveFirstNew)
                   );
            }
            catch (Exception ex)
            {
                return BaseResponse<byte[]>.Error(
                    message: nameof(RemoveFirstNew),
                    ex: ex
                );
            }
        }
        /// <summary>
        /// Su dung de gui cac packets co kich thuoc co đinh. hien khong su dung
        /// </summary>
        /// <param name="sourceByte"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static BaseResponse<byte[]> AddPaddingToBytes(byte[] sourceByte, int length = 1025)
        {
            try
            {
                byte[] bytes = new byte[length];
                int byteNeededToAdd = Math.Max(0, length - sourceByte.Length);
                if (sourceByte.Length > length)
                {
                    return BaseResponse<byte[]>.Error(
                        message: nameof(AddPaddingToBytes),
                        ex: new ArgumentException("Data is bigger than buffer size", nameof(sourceByte))
                    );
                }
                if (byteNeededToAdd == 0)
                {
                    return BaseResponse<byte[]>.Success(
                       message: nameof(AddPaddingToBytes),
                       data: sourceByte
                    );
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
                return BaseResponse<byte[]>.Success(
                    message: nameof(AddPaddingToBytes),
                    data: bytes
                );
            }
            catch (Exception ex)
            {
                return BaseResponse<byte[]>.Error(
                    message: nameof(AddPaddingToBytes),
                    ex: ex
                );
            }
        }
        /// <summary>
        /// Compress DeflateStream
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static BaseResponse<byte[]> Compress(byte[] data)
        {
            if (data == null || data.Length == 0)
                return BaseResponse<byte[]>.Error(
                    message: nameof(Combine),
                    ex: new ArgumentException("Data cannot be null or empty")
                 );

            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    using (DeflateStream dstream = new DeflateStream(stream, CompressionMode.Compress, true))
                    {
                        dstream.Write(data, 0, data.Length);
                    }
                    return BaseResponse<byte[]>.Success(
                        data: stream.ToArray(),
                        message: nameof(Compress)
                    );
                }
            }
            catch (Exception ex)
            {
                return BaseResponse<byte[]>.Error(
                    message: nameof(Compress),
                    ex: ex
                );
            }
        }
        public static BaseResponse<byte[]> CompressDeflate(byte[] data)
        {
            if (data == null || data.Length == 0)
                return BaseResponse<byte[]>.Error(
                    message: nameof(Combine),
                    ex: new ArgumentException("Data cannot be null or empty")
                ); ;
            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    using (var compressionStream = new DeflateStream(stream, CompressionMode.Compress))
                    {
                        compressionStream.Write(data, 0, data.Length);
                    }
                    return BaseResponse<byte[]>.Success(
                        data: stream.ToArray(),
                        message: nameof(CompressDeflate)
                    );
                }
            }
            catch (Exception ex)
            {
                return BaseResponse<byte[]>.Error(
                    message: nameof(CompressDeflate),
                    ex: ex
                );
            }
        }
        public static BaseResponse<byte[]> DeCompressDeflate(byte[] data)
        {
            try
            {
                using (MemoryStream input = new MemoryStream(data))
                using (MemoryStream output = new MemoryStream())
                using (var compressionStream = new DeflateStream(input, CompressionMode.Decompress))
                {
                    compressionStream.CopyTo(output);
                    return BaseResponse<byte[]>.Success(
                        data: output.ToArray(),
                        message: nameof(DeCompressDeflate)
                    );
                }
            }
            catch (Exception ex)
            {
                return BaseResponse<byte[]>.Error(
                    message: nameof(DeCompressDeflate),
                    ex: ex
                );
            }
        }
        public static BaseResponse<byte[]> CompressGZip(byte[] data)
        {
            if (data == null || data.Length == 0)
                return BaseResponse<byte[]>.Error(
                    message: nameof(Combine),
                    ex: new ArgumentException("Data cannot be null or empty")
                ); ;
            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    using (var compressionStream = new GZipStream(stream, CompressionMode.Compress))
                    {
                        compressionStream.Write(data, 0, data.Length);
                    }
                    return BaseResponse<byte[]>.Success(
                        data: stream.ToArray(),
                        message: nameof(CompressGZip)
                    );
                }
            }
            catch (Exception ex)
            {
                return BaseResponse<byte[]>.Error(
                    message: nameof(CompressGZip),
                    ex: ex
                );
            }
        }
        public static BaseResponse<byte[]> Decompress(byte[] data)
        {
            try
            {
                MemoryStream input = new MemoryStream(data);
                MemoryStream output = new MemoryStream();
                using (DeflateStream dsStream = new DeflateStream(input, CompressionMode.Decompress, true))
                {
                    dsStream.CopyTo(output);
                }
                return BaseResponse<byte[]>.Success(
                        data: output.ToArray(),
                        message: nameof(Decompress)
                    );
            }
            catch (Exception ex)
            {
                return BaseResponse<byte[]>.Error(
                    message: nameof(Decompress),
                    ex: ex
                );
            }
        }
        public static BaseResponse<byte[]> DecompressGZip(byte[] data)
        {

            try
            {
                using (MemoryStream input = new MemoryStream(data))
                using (MemoryStream output = new MemoryStream())
                using (var compressionStream = new GZipStream(input, CompressionMode.Decompress))
                {
                    compressionStream.CopyTo(output);
                    return BaseResponse<byte[]>.Success(
                        data: output.ToArray(),
                        message: nameof(DecompressGZip)
                    );
                }
            }
            catch (Exception ex)
            {
                return BaseResponse<byte[]>.Error(
                    message: nameof(DecompressGZip),
                    ex: ex
                );
            }
        }
        /// <summary>
        /// Merge two byte array to one array and return new array
        /// </summary>
        /// <param name="firstArray"></param>
        /// <param name="secondArray"></param>
        /// <returns></returns>
        public static BaseResponse<byte[]> Combine(byte[] firstArray, byte[] secondArray)
        {
            try
            {
                if (firstArray == null || secondArray == null || firstArray.Length == 0 || secondArray.Length == 0)
                    return BaseResponse<byte[]>.Error(
                         message: nameof(Combine),
                         ex: new ArgumentException("firstArray byte array and secondArray must be not null or empty")
                    );

                byte[] combineArray = new byte[firstArray.Length + secondArray.Length];
                Buffer.BlockCopy(firstArray, 0, combineArray, 0, firstArray.Length);
                Buffer.BlockCopy(secondArray, 0, combineArray, firstArray.Length, secondArray.Length);

                return BaseResponse<byte[]>.Success(
                   message: nameof(Combine),
                   data: combineArray
                );
            }
            catch (Exception ex)
            {
                return BaseResponse<byte[]>.Error(
                   message: nameof(Combine),
                   ex: ex
                );
            }
        }
        /// <summary>
        /// Split byte array to List<byte[]> with each item has size = size, except last item
        /// </summary>
        /// <param name="source"></param>
        /// <param name="length"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public static BaseResponse<List<byte[]>> ToListByteArray(byte[] source, int length, int size)
        {
            try
            {
                if (source == null || source.Length == 0)
                    return BaseResponse<List<byte[]>>.Error(
                         message: nameof(ToListByteArray),
                         ex: new ArgumentException("Source byte array, length, and size must be greater than zero.")
                    );

                if (length <= 0 || length > source.Length)
                    return BaseResponse<List<byte[]>>.Error(
                        message: nameof(ToListByteArray),
                        ex: new ArgumentOutOfRangeException(nameof(length), $"Length must be > 0 and <= source.Length ({source.Length}).")
                    );

                if (size <= 0)
                    return BaseResponse<List<byte[]>>.Error(
                        message: nameof(ToListByteArray),
                        ex: new ArgumentOutOfRangeException(nameof(size), "Size must be greater than 0.")
                    );

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

                return BaseResponse<List<byte[]>>.Success(
                    data: list,
                    message: nameof(ToListByteArray)
                );
            }
            catch (Exception ex)
            {
                return BaseResponse<List<byte[]>>.Error(
                   message: nameof(ToListByteArray),
                   ex: ex
                );
            }
        }
        /// <summary>
        /// Get data from file and convert to byte array
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static BaseResponse<byte[]> FileToByteArray(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return BaseResponse<byte[]>.Error(
                    message: nameof(FileToByteArray),
                    ex: new ArgumentException($"The file at {filePath} does not exist.")
                );
            }
            if (!File.Exists(filePath))
                return BaseResponse<byte[]>.NotFound(
                    message: nameof(FileToByteArray),
                    ex: new FileNotFoundException($"The file at {filePath} does not exist.")
                );
            try
            {
                using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    byte[] fileData = new byte[stream.Length];
                    int totalBytesRead = 0;
                    int byteRead;

                    while (totalBytesRead < stream.Length)
                    {
                        byteRead = stream.Read(fileData, totalBytesRead, fileData.Length - totalBytesRead);
                        if (byteRead == 0)
                        {
                            break;
                        }
                        totalBytesRead += byteRead;
                    }
                    return BaseResponse<byte[]>.Success(
                        data: fileData,
                        message: "File converted to byte array successfully"
                    );
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                return BaseResponse<byte[]>.Unauthorized
                (
                    message: nameof(FileToByteArray),
                    ex: ex
                );
            }
            catch (IOException ex)
            {
                return BaseResponse<byte[]>.Error
                (
                    message: nameof(FileToByteArray),
                    ex: ex
                );
            }
        }
    }
}
