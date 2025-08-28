using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Utils
{
    public class Test<T, TPriority> where TPriority : IComparable<TPriority>
    {
        public Test(T t, TPriority i) 
        {
            Value = t;
            Priority = i;
        }
        public T Value { get; set; }
        public TPriority Priority { get; set; }
    }
    public class VPriorityQueue<T, TPriority> where TPriority : IComparable<TPriority>
    {
        private readonly object _lock= new object();
        private readonly List<Test<T, TPriority>> _list;
        private readonly IComparer<TPriority> _comparer;
        public VPriorityQueue(IComparer<TPriority> comparer = null)
        {
            _list = new List<Test<T, TPriority>>();
            _comparer = comparer ?? Comparer<TPriority>.Default;
        }
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _list.Count;
                }
            }
        }
        /// <summary>
        /// Add item in VPriority queue
        /// </summary>
        /// <param name="t"></param>
        /// <param name="i"></param>
        public void Enqueue(T t, TPriority i)
        {
            lock (_lock)
            {
                _list.Add(new Test<T, TPriority>(t, i));

                int index = _list.Count - 1;
                while (index > 0)
                {
                    int parent = (index - 1) / 2;

                    if (_comparer.Compare(_list[index].Priority, _list[parent].Priority) >= 0) break;

                    var temp = _list[index];
                    _list[index] = _list[parent];
                    _list[parent] = temp;

                    index = parent;
                }
            }
        }
        /// <summary>
        /// Take item out VPriority queue
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool Dequeue(out T value)
        {
            lock (_lock)
            {
                value = default(T);
            if (_list.Count == 0) return false;

            var root = _list[0];
            _list[0] = _list[_list.Count - 1];
            _list.RemoveAt(_list.Count - 1);

            int index = 0;
            while (true)
            {
                int left = 2 * index + 1;
                int right = 2 * index + 2;
                int smallest = index;

                if (left < _list.Count && _comparer.Compare(_list[left].Priority, _list[smallest].Priority) < 0)
                    smallest = left;
                if (right < _list.Count && _comparer.Compare(_list[right].Priority, _list[smallest].Priority) < 0 )
                    smallest = right;


                if (index == smallest)
                    break;

                var temp = _list[index];
                _list[index] = _list[smallest];
                _list[smallest] = temp;

                index = smallest;
            }

            value = root.Value;
            return true;
            }
        }
    }
}
