using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace VRemoteDesktop.Helpers
{
    public interface IListByteArray
    {
        int Length { get; }
        List<ArraySegment<byte>> ListArray { get; }
        ArraySegment<byte> GetByIndex(int index);
        void AddInt32(int input);
        void AddBytes(byte[] input);
        void AddBytes(byte[] input, int offset, int length);
        void InsertFirst(byte[] input);
        void InsertFirst(byte[] input, int offset, int length);
        byte[] ToByteArray();
        void Clear();
    }
    public class ListByteArray: IListByteArray
    {
        internal List<ArraySegment<byte>> _list;
        private int _length;
        public ListByteArray()
        {
            _list = new List<ArraySegment<byte>>();
            _length = 0;
        }
        public int Length => _length;
        public List<ArraySegment<byte>> ListArray => _list;
        public ArraySegment<byte> GetByIndex(int index)
        {
            return _list[index];
        }
        public void AddInt32(int input)
        {
            _list.Add(new ArraySegment<byte>(BitConverter.GetBytes(input)));
            checked
            {
                _length += 4;
            }
        }
        public void AddBytes(byte[] input)
        {
            checked
            {
                if (input != null)
                {
                    AddInt32(input.Length);
                    _list.Add(new ArraySegment<byte>(input));
                    _length = _length + input.Length;
                    return;
                }
            }
        }
        public void AddBytes(byte[] input, int offset, int length)
        {
            checked
            {
                AddInt32(length);
                _list.Add(new ArraySegment<byte>(input, offset, length));
                _length += length;
            }
        }
        public void InsertFirst(byte[] input)
        {
            _list.Insert(0, new ArraySegment<byte>(input));
            checked
            {
                _length += input.Length;
            }
        }
        public void InsertFirst(byte[] input, int offset, int length)
        {
            _list.Insert(0, new ArraySegment<byte>(input, offset, length));
            checked
            {
                _length += length;
            }
        }
        public byte[] ToByteArray()
        {
            checked
            {
                byte[] array = new byte[_length];
                int copied = 0;
                foreach (ArraySegment<byte> item in _list)
                {
                    Buffer.BlockCopy(item.Array, item.Offset, array, copied, item.Count);
                    copied += item.Count;
                }
                return array;
            }
        }
        public void Clear()
        {
            _list.Clear();
            _length = 0;
        }
    }
}
