using NetFwTypeLib;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using VRemoteDesktop.Services.RemoteDesktop;

namespace VRemoteDesktop
{
    public class Startup
    {
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
            _remoteControlService = new RemoteService();
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
                if (_remoteControlService != null)
                    _remoteControlService.Dispose();
            }
        }
    }
}
