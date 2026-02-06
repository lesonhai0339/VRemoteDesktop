using System;
using System.Collections.Generic;

/// <summary>
/// A stack contains byte[] from 1KB to 1GB (2^10 to 2^30) shared buffer
/// </summary>
public static class VArrayPool
{
    private static readonly Stack<byte[]>[] _buckets = new Stack<byte[]>[20];
    private static readonly int _maxArraysPerBucket = 50;
    private static readonly object _lock = new object();

    static VArrayPool()
    {
        for (int i = 0; i < _buckets.Length; i++)
            _buckets[i] = new Stack<byte[]>();
    }

    public static byte[] Rent(int minimumLength)
    {
        try
        {
            int index = GetBucketIndex(minimumLength);

            if (index >= _buckets.Length)
                return new byte[minimumLength];

            lock (_lock)
            {
                if (_buckets[index].Count > 0)
                    return _buckets[index].Pop();
            }

            return new byte[1 << (index + 10)];
        }
        catch( Exception ex)
        {
            throw ex;
        }
    }

    public static void Return(byte[] array)
    {
        if (array == null) return;

        int index = GetBucketIndex(array.Length);

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