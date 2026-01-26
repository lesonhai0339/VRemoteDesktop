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
        private byte[] _buffer;
        //private IScreenCapture1 _capture;
        //private IScreenCaptureServiceListener _screenCaptureService;
        private IKeyboardService _keyboardHookService;
        //private VClientManager _vClientManager;
        //private GlobalHookService _globalHook;
        //private ClientInfoManager _clientInfoManager;
        //private RemoteDesktopService _remoteDesktopService;


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
            //sender rent buffer
            //var senderBuffer = VArrayPool.Rent(10 * 1024 * 1024);
            //var senderScreenTask = new ScreenTask(senderBuffer);
            //var sender = new VScreenSender(senderScreenTask);

            //var receiverBuffer = VArrayPool.Rent(10 * 1024 * 1024);
            //var receiverScreenTask = new ScreenTask(receiverBuffer);
            //var receiver = new VScreenReceiver(1920, 1080, receiverScreenTask);
            //sender.OnScreenCaptured += (s, e) =>
            //{
            //    try
            //    {
            //        receiver.DecompressedRawData(e.ScreenTask.Buffer, e.CompressedOffset, e.CompressedLength, (e.Type == Services.ScreenCapture.Enums.VScreenSenderEventType.FullScreen) ? true : false);
            //    }
            //    catch(Exception ex)
            //    {
            //        Console.WriteLine(ex.Message);
            //    }
            //    finally
            //    {
            //        e.ScreenTask.Complete();
            //    }
            //};

            //sender.GetFullScreen();
            //sender.Start();
            //Console.ReadLine(); 
            //int count = 0;
            //while (count < 300)
            //{
            //    Console.WriteLine("\n----------------------------\n");
            //    Thread.Sleep(1000);
            //}
            //VArrayPool.Return(senderBuffer);
            //VArrayPool.Return(receiverBuffer);
            //return;
            var bounds = Screen.PrimaryScreen.Bounds;
            _screenSender = new VScreenSender(bounds.Width, bounds.Height);
            _sessionManagement = new SessionManager();

            //_capture = new ScreenCapture1();
            _keyboardHookService = new KeyboardService();
            //_vClientManager = new VClientManager();
            //_clientInfoManager = new ClientInfoManager();
            //_screenCaptureService = new ScreenCaptureService(_capture);
            //_globalHook = new GlobalHookService(_keyboardHookService, _screenCaptureService);
            //_remoteDesktopService = new RemoteDesktopService(_screenSender, _globalHook, _sessionManagement, _clientInfoManager);


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
                VArrayPool.Return(_buffer);
                _remoteControlService?.Dispose();
            }
        }
    }
}
