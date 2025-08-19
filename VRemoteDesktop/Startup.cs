using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.Services.ConnectionManager;
using VRemoteDesktop.Services.Keyboard;
using VRemoteDesktop.Services.RemoteDesktop;
using VRemoteDesktop.Services.ScreenCapture;
using VRemoteDesktop.Services.SystemService;
using VRemoteDesktop.Services.VTCPClient;

namespace VRemoteDesktop
{
    public class Startup
    {
        private VClientManager _vClientManager;
        private GlobalHookService _globalhook;
        private ClientInfoManager _clientInfoManager;
        private RemoteDesktopService _remoteDesktopService;
        public Startup()
        {
            Initialize();
        }
        private void Initialize()
        {
            var keyboardhook = new KeyboardService();
            var screenhook = new ScreenCaptureService(null,null);
            _globalhook = new GlobalHookService(keyboardhook, screenhook);
            _vClientManager = new VClientManager();
            _clientInfoManager = new ClientInfoManager();
            _remoteDesktopService = new RemoteDesktopService(_globalhook, _vClientManager, _clientInfoManager);
        }
        public void Run()
        {
            try
            {
                FormMain frmMain = new FormMain(_remoteDesktopService);
                Application.Run(frmMain);
            }
            finally
            {
                _remoteDesktopService.Dispose();
            }
        }
    }
}
