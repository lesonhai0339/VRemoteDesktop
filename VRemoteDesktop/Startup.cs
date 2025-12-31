using NetFwTypeLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
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
        private IScreenCapture1 _capture;
        private IScreenCaptureServiceListener _screenCaptureService;
        private IKeyboardService _keyboardHookService;
        private VClientManager _vClientManager;
        private GlobalHookService _globalHook;
        private ClientInfoManager _clientInfoManager;
        private RemoteDesktopService _remoteDesktopService;
        public Startup()
        {
            RegisterFirewallAccess();

            Initialize();
        }
        private void RegisterFirewallAccess()
        {
            try
            {
                string ruleName = "Vsign4_RemoteDesktop";

                if (CheckRuleExisted(ruleName)) return;

                string applicationName = Process.GetCurrentProcess().MainModule.FileName;

                CreateRule(ruleName, applicationName, NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_TCP);
                CreateRule(ruleName, applicationName, NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_UDP);  
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }
        private void CreateRule(string ruleName, string applicationName, NET_FW_IP_PROTOCOL_ protocol)
        {
            INetFwPolicy2 policy =
                    (INetFwPolicy2)Activator.CreateInstance(
                        Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));

            INetFwRule rule =
                (INetFwRule)Activator.CreateInstance(
                    Type.GetTypeFromProgID("HNetCfg.FWRule"));

            rule.Name = ruleName;
            rule.Description = "Access for app to open port";
            rule.Enabled = true;
            rule.ApplicationName = applicationName;
            rule.Protocol = (int)protocol;
            rule.Action = NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
            rule.Direction = NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN;
            rule.InterfaceTypes = "All";

            rule.LocalAddresses = "*";
            rule.RemoteAddresses = "*";


            rule.Profiles = 6; //Private and Public


            policy.Rules.Add(rule);
        }
        private bool CheckRuleExisted(string ruleName)
        {
            INetFwPolicy2 policy =
                   (INetFwPolicy2)Activator.CreateInstance(
                       Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));

            foreach(INetFwRule rule in policy.Rules)
            {
                if (string.Compare(rule.Name, ruleName, StringComparison.OrdinalIgnoreCase) == 0)
                    return true;
            }
            return false;
        }
        private void Initialize()
        {
            _capture = new ScreenCapture1();
            _keyboardHookService = new KeyboardService();
            _vClientManager = new VClientManager();
            _clientInfoManager = new ClientInfoManager();
            _screenCaptureService = new ScreenCaptureService(_capture);
            _globalHook = new GlobalHookService(_keyboardHookService, _screenCaptureService);
            _remoteDesktopService = new RemoteDesktopService(_globalHook, _vClientManager, _clientInfoManager);
        }
        public void Run()
        {
            try
            {
                Thread.CurrentThread.CurrentUICulture = new CultureInfo("vi");
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
