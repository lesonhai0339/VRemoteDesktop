using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VRemoteServer.RelayServer.Domains
{
    public interface ITrackableDisposable : IDisposable
    {
        bool IsDisposed { get; }

        /// <summary>
        /// Atomically claims this object for teardown. Returns true only for the FIRST caller,
        /// so exactly one thread runs the close/cleanup path (prevents double free of pooled
        /// resources when several disconnect signals race).
        /// </summary>
        bool TryBeginDispose();
    }
}
