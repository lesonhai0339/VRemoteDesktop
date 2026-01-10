using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Utils
{
    public static class ByteArrayPool
    {
        private static readonly Stack<byte[]> _pool = new Stack<byte[]>();
        private static readonly object _lock = new object();
        private static readonly int _maxPoolSize = 50;  

        public static byte[] Rent(int minimumLength)
        {
            lock (_lock)
            {
                if(_pool.Count > 0)
                {
                    var buffer = _pool.Pop();
                    if (buffer.Length >= minimumLength)
                        return buffer;

                    _pool.Push(buffer);
                }
            }
            return new byte[minimumLength];
        }
        public static void Return(byte[] buffer)
        {
            if (buffer == null)
                return;

            lock (_lock)
            {
                if (_pool.Count < _maxPoolSize)
                    _pool.Push(buffer);
            }
        }
    }
}
