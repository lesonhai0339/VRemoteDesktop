using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.DTOs;

namespace VRemoteServer.RelayServer.Domains
{
    /// <summary>
    /// Base manager class for thread-safe
    /// </summary>
    public interface IBaseManagement<T>
    {
        int Count { get; }
        bool Add(string id, T obj);
        T Get(string id);
        T Get(string id, T obj);
        T Get(Predicate<T> predicate);
        T GetFirst(Func<T, bool> predicate);
        IEnumerable<T> GetAll();
        bool Get(string id, out T obj);
        IEnumerable<T> GetAll(Predicate<T> predicate);
        string GetIdByValue(T obj);
        bool Update(string id, T obj);
        bool Remove(string id);
        bool Remove(KeyValuePair<string, T> obj);
        bool Remove(T obj);
        bool Remove(Predicate<T> predicate);
        T TakeAndRemove(string id);
        bool TakeAndRemove(string id, out T obj);
        IEnumerable<T> GetByObject(object obj);
        T AddOrUpdate(string id, T obj);
        void Dispose();
    }
    public abstract class BaseManagement<T> : IBaseManagement<T>, IDisposable where T: class
    {
        private bool _disposed;
        protected readonly ConcurrentDictionary<string, T> _keyValuePairs;
        public BaseManagement()
        {
            _disposed = false;
            _keyValuePairs = new ConcurrentDictionary<string, T>();
        }

        #region Methods
        public int Count => _keyValuePairs.Count;

        public virtual bool Add(string id, T obj)
            => _keyValuePairs.TryAdd(id, obj);

        public virtual T Get(string id)
            => _keyValuePairs.TryGetValue(id, out var result) ? result : null;

        public virtual T Get(string id, T obj)
            => _keyValuePairs.GetOrAdd(id, obj);

        public virtual T AddOrUpdate(string id, T obj)
        {
            return _keyValuePairs.AddOrUpdate(id, 
                addValueFactory: _ => obj, 
                updateValueFactory: (_, __) =>  obj);
        }
        public virtual T Get(Predicate<T> predicate)
        {
            foreach(var v in _keyValuePairs.Values)
            {
                if (v != null && predicate(v))
                    return v;
            }
            return null;
        }

        public virtual T GetFirst(Func<T, bool> predicate)
        {
            return _keyValuePairs.Values.FirstOrDefault(x => predicate(x));
        }

        public virtual IEnumerable<T> GetAll(Predicate<T> predicate)
        {
            return _keyValuePairs.Values.Where(x => predicate(x));
        }

        public virtual bool Get(string id, out T obj)
           => _keyValuePairs.TryGetValue(id,out obj) ? true : false;

        public virtual IEnumerable<T> GetAll()
            => _keyValuePairs.Values;

        public virtual string GetIdByValue(T obj)
            => _keyValuePairs.FirstOrDefault(x => ReferenceEquals(x.Value, obj)).Key;

        public virtual bool Update(string id, T newObj)
            => _keyValuePairs.AddOrUpdate(id, addValueFactory: _ => newObj, updateValueFactory: (_, old) => newObj) != null;

        public virtual bool Remove(string id)
            => _keyValuePairs.TryRemove(id, out _);

        public virtual bool Remove(KeyValuePair<string, T> obj)
            => _keyValuePairs.TryRemove(obj);

        public virtual bool Remove(T obj)
        {
            bool removed = false;

            foreach (var kvp in _keyValuePairs.ToArray())
            {
                if (ReferenceEquals(kvp.Value, obj))
                {
                    if (_keyValuePairs.TryRemove(kvp.Key, out _))
                    {
                        removed = true;
                    }
                }
            }

            return removed;
        }

        public virtual bool Remove(Predicate<T> predicate)
        {
            bool removed = false;

            foreach (var kvp in _keyValuePairs.ToArray())
            {
                if (predicate(kvp.Value))
                {
                    if (_keyValuePairs.TryRemove(kvp.Key, out _))
                    {
                        removed = true;
                    }
                }
            }

            return removed;
        }

        public virtual T TakeAndRemove(string id)
            => _keyValuePairs.TryRemove(id, out var result) ? result : null;

        public virtual bool TakeAndRemove(string id, out T obj)
           => _keyValuePairs.TryRemove(id, out obj) ? true : false;

        public abstract IEnumerable<T> GetByObject(object obj);

        #endregion

        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing || _disposed) return;

            try
            {
                _keyValuePairs.Clear();
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}
