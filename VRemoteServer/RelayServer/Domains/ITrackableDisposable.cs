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
    }
}
