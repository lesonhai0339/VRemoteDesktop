using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Forms;
using VRemoteClient.Models.DTOs;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace VRemoteClient.Utils
{
    public static class ByteArrayUtils
    {
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
            catch(Exception ex)
            {
                return BaseResponse<byte[]>.Error(
                   message: nameof(Combine),
                   ex: ex
                );
            }
        }
        public static BaseResponse<List<byte[]>> ToListByteArray(byte[] source , int length, int size)
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
            catch(Exception ex)
            {
                return BaseResponse<List<byte[]>>.Error(
                   message: nameof(ToListByteArray),
                   ex: ex
                );
            }
        }
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
