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
        private IScreenCapture _capture;
        private IScreenCaptureServiceListener _screenCaptureService;
        private IKeyboardService _keyboardHookService;
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
            _capture = new ScreenCapture();
            _keyboardHookService = new KeyboardService();
            _vClientManager = new VClientManager();
            _clientInfoManager = new ClientInfoManager();
            _screenCaptureService = new ScreenCaptureService(_capture);
            _globalhook = new GlobalHookService(_keyboardHookService, _screenCaptureService);
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
                _remoteDesktopService?.Dispose();
            }
        }
    }
}
