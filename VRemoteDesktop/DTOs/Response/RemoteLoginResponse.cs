using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Services.Machine.DTOs;

namespace VRemoteDesktop.DTOs.Response
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
        public string ConnectionId { get; set; }
        public MachineInfo MachineInfo { get; set; }
    }
}
