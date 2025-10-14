using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRemoteDesktop.Enums;
using static VRemoteDesktop.Utils.DefaultSocketPacket;

namespace VRemoteDesktop.Utils
{
    public interface ICusQueue<T>
    {
        int Count { get; }
        bool HasItem();
        void Enqueue(T tasks, QueuePriority priority);
        bool Dequeue(out T task);
        int RemoveAll(Func<T, bool> match);
        int RemoveAll(QueuePriority priority, Func<T, bool> match);
        void Dispose();
    }
    public class CusQueue<T>: ICusQueue<T>, IDisposable
    {
        private bool _disposing = false;
        private bool _disposed = false;
        private readonly object _highLock = new object();
        private readonly object _mediumLock = new object();
        private readonly object _lowLock = new object();
        private DateTimeOffset _lastFileSend;
        private int _limitPacketSendPerSecond;
        private ConcurrentQueue<T> _highTasks;
        private ConcurrentQueue<T> _mediumTasks;
        private ConcurrentQueue<T> _lowTasks;
        private Dictionary<QueuePriority, ConcurrentQueue<T>> _keyValuePairs;
        private Dictionary<QueuePriority, object> _locks;
        public CusQueue()
        {
            _lastFileSend = DateTimeOffset.UtcNow;
            _limitPacketSendPerSecond = 1000 / (LIMIT_BANDWIDTH_PER_SECOND / DEFAULT_CHUNK_SIZE);
            _highTasks = new ConcurrentQueue<T>();
            _mediumTasks = new ConcurrentQueue<T>();
            _lowTasks = new ConcurrentQueue<T>();
            _keyValuePairs = new Dictionary<QueuePriority, ConcurrentQueue<T>>()
            {
                { QueuePriority.High, _highTasks},
                { QueuePriority.Medium, _mediumTasks},
                { QueuePriority.Low, _lowTasks}
            };
            _locks = new Dictionary<QueuePriority, object>()
            {
                { QueuePriority.High, _highLock},
                { QueuePriority.Medium, _mediumLock},
                { QueuePriority.Low, _lowLock}
            };
        }
        public int Count =>
            _highTasks.Count + _mediumTasks.Count + _lowTasks.Count;
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
            lock (_locks[priority])
            {
                _keyValuePairs[priority].Enqueue(tasks);
            }
        }
        public bool Dequeue(out T task)
        {
            task = default(T);

            if (IsDispose()) return false;

            lock (_highLock)
            {
                if (_highTasks.TryDequeue(out task)) return true;
            }
            lock (_mediumLock)
            {
                if (_mediumTasks.TryDequeue(out task)) return true;
            }
            lock (_lowLock)
            {
                if (_lowTasks.Count > 0)
                {
                    DateTimeOffset now = DateTimeOffset.UtcNow;
                    var elapsed = (now - _lastFileSend).TotalMilliseconds;
                    if (elapsed >= _limitPacketSendPerSecond)
                    {
                        _lastFileSend = now;
                        if (_lowTasks.TryDequeue(out task)) return true;
                    }
                }
            }
            return false;
        }
        public int RemoveAll(QueuePriority priority, Func<T, bool> match)
        {
            int removed = RemoveAllOnSpecificQueue(_keyValuePairs[priority],_locks[priority], match);
            return removed;
        }
        private int RemoveAllOnSpecificQueue(ConcurrentQueue<T> sourceQueue, object @lock, Func<T, bool> match)
        {
            if (IsDispose()) return 0;

            if (match == null) return 0;

            lock (@lock)
            {
                int removed = 0;

                void FilterQueue(ConcurrentQueue<T> queue)
                {
                    var tempQueue = new ConcurrentQueue<T>();
                    while (queue.TryDequeue(out T task))
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

                    while (tempQueue.TryDequeue(out T task))
                    {
                        queue.Enqueue(task);
                    }
                }
                FilterQueue(sourceQueue);
                return removed;
            }
        }
        public int RemoveAll(Func<T, bool> match)
        {
            if (IsDispose()) return 0;

            if (match == null) return 0 ;

            int removed = 0;

            void FilterQueue(ConcurrentQueue<T> queue)
            {
                var tempQueue = new ConcurrentQueue<T>();
                while (queue.TryDequeue(out T task))
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

                while (tempQueue.TryDequeue(out T task))
                {
                    queue.Enqueue(task);
                }
            }

            lock (_highLock)
            {
                FilterQueue(_highTasks);
            }
            lock (_mediumLock)
            {
                FilterQueue(_mediumTasks);
            }
            lock (_lowLock)
            {
                FilterQueue(_lowTasks);
            }
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
                    _locks.Clear();
                    _locks = null;
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
