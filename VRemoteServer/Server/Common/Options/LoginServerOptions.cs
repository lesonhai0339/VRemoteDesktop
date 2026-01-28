using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VRemoteServer.RelayServer.Common.Options
{
    public class LoginServerOptions
    {
        public int MaxConnections { get; set; } = 1000;
        public int MaxBufferSize { get; set; } = 8192;
    }
}
