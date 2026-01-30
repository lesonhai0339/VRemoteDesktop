using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Services.Machine.DTOs;

namespace VRemoteDesktop.DTOs.Requests
{
    public class ConnectionCredentials
    {
        public ConnectionCredentials(string partnerId, string partnerPassword, ControlType type, MachineInfo machineInfo)
        {
            PartnerId = partnerId;
            PartnerPassword = partnerPassword;
            Type = type;
            MachineInfo = machineInfo;
        }
    
        public string PartnerId { get; private set; }
        public string PartnerPassword { get;private set; }  
        public ControlType Type { get; private set; }
        public MachineInfo MachineInfo { get; private set; }    
    }
}
