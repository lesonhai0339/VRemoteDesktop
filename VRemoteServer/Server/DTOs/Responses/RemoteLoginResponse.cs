using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.Enums;

namespace VRemoteServer.RelayServer.DTOs.Responses
{
    public class RemoteLoginResponse
    {
        public RemoteLoginResponse(bool loggedIn, string connectionId, MachineInfo machineInfo)
        {
            LoggedIn = loggedIn;
            ConnectionId = connectionId;
            MachineInfo = machineInfo;
        }

        public bool LoggedIn { get; set; }
        public string ConnectionId { get;set; }
        public MachineInfo MachineInfo { get; set; }
    }
}
