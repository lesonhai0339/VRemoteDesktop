using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemoteClient.Remote
{
    public class ClientClass
    {
        private SocketRemoteClient _remoteClient;
        public ClientClass(SocketRemoteClient remoteCLient)
        {
            _remoteClient = remoteCLient;
        }

    }
}
