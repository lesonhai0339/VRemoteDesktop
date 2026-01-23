using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Services.Machine.DTOs;

namespace VRemoteDesktop.Services.RemoteDesktop
{
    public partial class RemoteService
    {
        #region System
        public MachineInfo GetMachineInfo()
        {
            return _machineProfile.MachineInfo; 
        }
        public void UpdatePublicIp(string publicIp)
        {
            _machineProfile.UpdatePublicIp(publicIp);
        }
        public void UpdateLocalIp(string localIp)
        {
            _machineProfile.UpdateLocalIp(localIp);
        }
        public void UpdatePort(string port)
        {
            _machineProfile.UpdatePort(port);
        }
        #endregion
    }
}
