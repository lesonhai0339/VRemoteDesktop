using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.Services.VTCPClientManager;

namespace VRemoteDesktop
{
    public class Startup
    {
        private VTCPClientManagerService _vtcpClientManagerService;
        public Startup()
        {
            Initialize();
        }
        private void Initialize()
        {
            _vtcpClientManagerService = new VTCPClientManagerService();
        }
        public void Run()
        {
            FormMain frmMain = new FormMain(_vtcpClientManagerService);
            Application.Run(frmMain);
        }
    }
}
