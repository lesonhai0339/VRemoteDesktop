using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vsign4.VRemoteDesktop.Utils
{
    public static class VArrayPool
    {
        // 20 buckets bao phủ từ 1KB đến 1GB (2^10 đến 2^30)
        private static readonly Stack<byte[]>[] _buckets = new Stack<byte[]>[20];
        private static readonly int _maxArraysPerBucket = 50;
        private static readonly object _lock = new object();

        static VArrayPool()
        {
            for (int i = 0; i < _buckets.Length; i++)
                _buckets[i] = new Stack<byte[]>();
        }

        /// <summary>
        /// Thuê một mảng byte có kích thước ít nhất bằng minimumLength.
        /// Kích thước thực tế trả về sẽ là lũy thừa của 2 (1024, 2048, 4096...).
        /// </summary>
        public static byte[] Rent(int minimumLength)
        {
            try
            {
                int index = GetBucketIndex(minimumLength);

                // Nếu vượt quá khả năng quản lý (1GB), tạo mới mảng thô (không cho vào pool)
                if (index >= _buckets.Length)
                    return new byte[minimumLength];

                lock (_lock)
                {
                    if (_buckets[index].Count > 0)
                        return _buckets[index].Pop();
                }

                // Tạo mảng mới theo kích thước chuẩn của Bucket (2^(index + 10))
                return new byte[1 << (index + 10)];
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Trả mảng về Pool sau khi sử dụng xong.
        /// </summary>
        public static void Return(byte[] array)
        {
            if (array == null) return;

            int index = GetBucketIndex(array.Length);

            // Chỉ nhận lại các mảng có kích thước chuẩn của bucket (lũy thừa của 2)
            // Điều này đảm bảo Pool không bị lộn xộn bởi các mảng tạo lẻ bên ngoài
            if (index < _buckets.Length && (array.Length == (1 << (index + 10))))
            {
                lock (_lock)
                {
                    if (_buckets[index].Count < _maxArraysPerBucket)
                    {
                        _buckets[index].Push(array);
                    }
                }
            }
        }

        /// <summary>
        /// Nạp sẵn một số lượng mảng vào các bucket quan trọng để tránh "lag" lần đầu.
        /// </summary>
        public static void PreFill(int size, int count)
        {
            int index = GetBucketIndex(size);
            if (index >= _buckets.Length) return;

            lock (_lock)
            {
                int arraySize = 1 << (index + 10);
                while (_buckets[index].Count < count)
                {
                    _buckets[index].Push(new byte[arraySize]);
                }
            }
        }

        private static int GetBucketIndex(int length)
        {
            if (length <= 1024) return 0;
            int index = 0;
            int val = (length - 1) >> 10;
            while (val > 0)
            {
                val >>= 1;
                index++;
            }
            return index;
        }
    }
}
