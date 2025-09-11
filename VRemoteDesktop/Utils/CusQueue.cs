using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Models;

namespace VRemoteDesktop.Utils
{
    public interface ICusQueue<T>
    {
        bool HasItem();
        void Enqueue(T tasks, QueuePriority priority);
        bool Dequeue(out T task);
        int RemoveAll(Func<T, bool> match);
        void Dispose();
    }
    public class CusQueue<T>: ICusQueue<T>, IDisposable
    {
        private bool _disposing = false;
        private bool _disposed = false;
        private readonly object _lock = new object();
        private ConcurrentQueue<T> _highTasks;
        private ConcurrentQueue<T> _mediumTasks;
        private ConcurrentQueue<T> _lowTasks;
        private Dictionary<QueuePriority, ConcurrentQueue<T>> _keyValuePairs;
        public CusQueue()
        {
            _highTasks = new ConcurrentQueue<T>();
            _mediumTasks = new ConcurrentQueue<T>();
            _lowTasks = new ConcurrentQueue<T>();
            _keyValuePairs = new Dictionary<QueuePriority, ConcurrentQueue<T>>()
            {
                { QueuePriority.High, _highTasks},
                { QueuePriority.Medium, _mediumTasks},
                { QueuePriority.Low, _lowTasks}
            };
        }
        public bool HasItem()
        {
            return _highTasks.TryPeek(out _) 
                || _mediumTasks.TryPeek(out _) 
                || _lowTasks.TryPeek(out _);
        }
        private bool IsDispose()
        {
            return _disposed || _disposing;
        }
        public void Enqueue(T tasks, QueuePriority priority)
        {
            if (IsDispose()) return;

            if (tasks == null) return;

            _keyValuePairs[priority].Enqueue(tasks);
        }
        public bool Dequeue(out T task)
        {
            task = default(T);

            if (IsDispose()) return false;

            if (_highTasks.TryDequeue(out task)) return true;
            if (_mediumTasks.TryDequeue(out task)) return true;
            if (_lowTasks.TryDequeue(out task)) return true;

            return false;
        }
        public int RemoveAll(Func<T, bool> match)
        {
            if (IsDispose()) return 0;

            if (match == null) return 0 ;

            int removed = 0;

            void FilterQueue(ConcurrentQueue<T> queue)
            {
                var tempQueue = new ConcurrentQueue<T>();
                while(queue.TryDequeue(out T task))
                {
                    if (match(task))
                    {
                        removed++;
                    }
                    else
                    {
                        tempQueue.Enqueue(task);
                    }
                }
                
                while(tempQueue.TryDequeue(out T task))
                {
                    queue.Enqueue(task);
                }
            }

            FilterQueue(_highTasks);
            FilterQueue(_mediumTasks);
            FilterQueue(_lowTasks);

            return removed;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposing = true;
                if (disposing)
                {
                    while (_highTasks.TryDequeue(out _));
                    while(_mediumTasks.TryDequeue(out _));
                    while(_lowTasks.TryDequeue(out _)) ;

                    _keyValuePairs.Clear();
                    _keyValuePairs = null;
                    _highTasks = null;
                    _mediumTasks = null;
                    _lowTasks = null;
                }
                _disposed = true;
                _disposing = false;
            }
        }
    }
}
