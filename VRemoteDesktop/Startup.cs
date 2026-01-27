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
using VRemoteDesktop.Services.Client;
using VRemoteDesktop.Services.ConnectionManager;
using VRemoteDesktop.Services.Keyboard;
using VRemoteDesktop.Services.RemoteDesktop;
using VRemoteDesktop.Services.ScreenCapture;
using VRemoteDesktop.Services.ScreenCapture.DTOs;
using VRemoteDesktop.Services.ScreenCapture.GDI;
using VRemoteDesktop.Services.SessionManagement;
using VRemoteDesktop.Services.SystemService;
using VRemoteDesktop.Services.VTCPClient;
using VRemoteDesktop.Utils;

namespace VRemoteDesktop
{
    public class Startup
    {
        private IKeyboardService _keyboardHookService;
        private IMachineProfile _machineProfile;
        private IVScreenSender _screenSender;
        private SessionManager _sessionManagement;
        private RemoteService _remoteControlService;


        public Startup()
        {
            //RegisterFirewallAccess();

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
            var bounds = Screen.PrimaryScreen.Bounds;
            _screenSender = new VScreenSender(bounds.Width, bounds.Height);
            _sessionManagement = new SessionManager();

            _keyboardHookService = new KeyboardService();

            _machineProfile = new MachineProfile();
            _remoteControlService = new RemoteService(_screenSender, _sessionManagement, _machineProfile, _keyboardHookService);
        }
        public void Run()
        {
            try
            {
                Thread.CurrentThread.CurrentUICulture = new CultureInfo("vi");
                FormMain frmMain = new FormMain(_remoteControlService);
                Application.Run(frmMain);
            }
            finally
            {
                _remoteControlService?.Dispose();
            }
        }
    }
}
