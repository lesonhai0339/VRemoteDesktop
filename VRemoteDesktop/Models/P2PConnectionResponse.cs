using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteServer.Models;

namespace VRemoteDesktop.Models
{
    public class P2PConnectionResponse
    {
        public P2PConnectionResponse(bool isLogged, ClientInfo connectorInfo)
        {
            IsLogged = isLogged;
            ConnectorInfo = connectorInfo;
        }

        public bool IsLogged { get; set; }
        public ClientInfo ConnectorInfo { get; set; }
    }
}
