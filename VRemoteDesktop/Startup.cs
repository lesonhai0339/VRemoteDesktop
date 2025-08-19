using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.Services.ConnectionManager;
using VRemoteDesktop.Services.Keyboard;
using VRemoteDesktop.Services.ScreenCapture;
using VRemoteDesktop.Services.SystemService;
using VRemoteDesktop.Services.VTCPClientManager;

namespace VRemoteDesktop
{
    public class Startup
    {
        private VTCPClientManagerService _vtcpClientManagerService;
        private GlobalHookService _globalhook;
        private ConnectionManager _connectionManager;
        public Startup()
        {
            Initialize();
        }
        private void Initialize()
        {
            var keyboardhook = new KeyboardService();
            var screenhook = new ScreenCaptureService(null,null);
            _globalhook = new GlobalHookService(keyboardhook, screenhook);
            _vtcpClientManagerService = new VTCPClientManagerService();
            _connectionManager = new ConnectionManager();
        }
        public void Run()
        {
            try
            {
                FormMain frmMain = new FormMain(_globalhook, _vtcpClientManagerService, _connectionManager);
                Application.Run(frmMain);
            }
            finally
            {
                _globalhook.Dispose();
                _vtcpClientManagerService.Dispose();
            }
        }
    }
}
