using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Services.RemoteDesktop;

namespace VRemoteDesktop.Presenters.DTOs
{
    public class NewRemoteConnection
    {
        public NewRemoteConnection(bool isController, ClientSession clientSession)
        {
            IsController = isController;
            ClientSession = clientSession;
        }
        public bool IsController { get; set; }
        public ClientSession ClientSession { get; set; }
    }
}
