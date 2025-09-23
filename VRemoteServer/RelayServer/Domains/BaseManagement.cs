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
        T Get(Predicate<T> predicate);
        bool Add(string id, T obj);
        T Get(string id);
        T Get(string id, T obj);
        string GetIdByValue(T obj);
        bool Update(string id, T obj);
        bool Remove(string id);
        T TakeAndRemote(string id);
        void Dispose();
    }
    public class BaseManagement<T> : IBaseManagement<T>, IDisposable where T: class
    {
        private bool _disposed;
        protected readonly ConcurrentDictionary<string, T> _keyValuePairs;
        public BaseManagement()
        {
            _disposed = false;
            _keyValuePairs = new ConcurrentDictionary<string, T>();
        }
        #region Properties
        #endregion
        #region Methods
        public bool Add(string id, T obj)
            => _keyValuePairs.TryAdd(id, obj);
        public T Get(string id)
            => _keyValuePairs.TryGetValue(id, out var result) ? result : null;
        public T Get(string id, T obj)
            => _keyValuePairs.GetOrAdd(id, obj);
        public T Get(Predicate<T> predicate)
        {
            foreach(var v in _keyValuePairs.Values)
            {
                if (v != null && predicate(v))
                    return v;
            }
            return null;
        }
        public string GetIdByValue(T obj)
            => _keyValuePairs.FirstOrDefault(x => ReferenceEquals(x.Value, obj)).Key;
        public bool Update(string id, T newObj)
            => _keyValuePairs.AddOrUpdate(id, addValueFactory: _ => newObj, updateValueFactory: (_, old) => newObj) != null;
        public bool Remove(string id)
            => _keyValuePairs.TryRemove(id, out _);
        public T TakeAndRemote(string id)
            => _keyValuePairs.TryRemove(id, out var result) ? result : null;
        #endregion
        #region Events
        #endregion
        public void Dispose()
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
