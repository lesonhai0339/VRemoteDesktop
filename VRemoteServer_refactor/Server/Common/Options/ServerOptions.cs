using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VRemoteServer.Server.Options
{
    public class ServerOptions
    {
        public int Timeout { get; set; } = 5;
        public int MaxTimeout { get; set; } = 300;
        public int RetryTime { get; set; } = 3;
        public int HeaderLength { get; set; } = 13;
    }
}
