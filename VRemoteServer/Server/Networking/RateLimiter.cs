using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VRemoteServer.RelayServer.Networking
{
    public class RequestSocket
    {
        public int Connections { get; set; }
        public DateTimeOffset LastActivity { get; set;}
    }
    public interface IRateLimiter
    {
        bool CanAccept(string ip);
        void Release(string ip);
        void Dispose(); 
    }   
    public class RateLimiter : IRateLimiter, IDisposable
    {
        private readonly int LIMIT_CONNECTION_PER_IP = 10;  
        private readonly ConcurrentDictionary<string, RequestSocket> _requestIp;
        private readonly Timer _timer;
        private int _disposed;
        public RateLimiter()
        {
            _disposed = 0;
            _requestIp = new ConcurrentDictionary<string, RequestSocket>();
            _timer = new Timer(Cleanup, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        private void Cleanup(object state)
        {
            if (_disposed == 1) return;


            var time = DateTimeOffset.UtcNow.AddMinutes(-10);
            var removeKeys = new List<string>();

            foreach(var kvp in _requestIp)
            {
                lock (kvp.Value)
                {
                    if(kvp.Value.Connections == 0 && kvp.Value.LastActivity < time)
                    {
                        removeKeys.Add(kvp.Key);
                    }
                }
            }

            foreach (var key in removeKeys)
            {
                _requestIp.TryRemove(key, out _);
            }
        }

        public bool CanAccept(string ip)
        {
            if (_disposed == 1) 
                return false;

            if (string.IsNullOrEmpty(ip))
                return false;

            var request = _requestIp.GetOrAdd(ip, valueFactory: _ => new RequestSocket { Connections = 0 , LastActivity = DateTimeOffset.UtcNow });

            lock (request)
            {
                if (request.Connections >= LIMIT_CONNECTION_PER_IP)
                {
                    return false;
                }

                request.Connections++;
                request.LastActivity = DateTimeOffset.UtcNow;   
                return true;
            }
        }
        public void Release(string ip)
        {
            if (_disposed == 1) 
                return;

            if (string.IsNullOrEmpty(ip))
                return;

            if (_requestIp.TryGetValue(ip, out var exists))
            {
                lock (exists)
                {
                    if (exists.Connections > 0)
                    {
                        exists.Connections--;
                        exists.LastActivity = DateTimeOffset.UtcNow;
                    }

                    if(exists.Connections == 0)
                        _requestIp.TryRemove(ip, out _);
                }
            }
        }   
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
            {
                _timer?.Dispose();
                _requestIp.Clear();
            }
        }
    }
}
