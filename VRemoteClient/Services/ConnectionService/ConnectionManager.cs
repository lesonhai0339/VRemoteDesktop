using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteClient.Models.Entities;

namespace VRemoteClient.Services.ConnectionService
{
    public static class ConnectionManager
    {
        private static ConcurrentDictionary<string, ConnectionInfo> _currentConnections = new ConcurrentDictionary<string, ConnectionInfo>();
        public static int NumberOfConnections => _currentConnections.Count;
        public static bool AddConnection(string sessionId, ConnectionInfo info)
        {
            bool flag = _currentConnections.TryAdd(sessionId, info);
            return flag;
        }
        public static bool RemoveConnection(string sessionId)
        {
            bool flag = _currentConnections.TryRemove(sessionId, out var _);
            return flag;
        }
        public static void Clear()
        {
            _currentConnections.Clear();
        }
    }
}
