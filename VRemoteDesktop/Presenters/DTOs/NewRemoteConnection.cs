using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Services.RemoteDesktop;

namespace VRemoteDesktop.Presenters.DTOs
{
    public class NewRemoteConnection
    {
        public NewRemoteConnection(ClientSession clientSession)
        {
            ClientSession = clientSession;
        }
        public ClientSession ClientSession { get; set; }
    }
}
