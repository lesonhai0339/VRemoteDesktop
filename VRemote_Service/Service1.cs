using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using Microsoft.Win32;

namespace VRemote_Service
{
    public partial class Service1 : ServiceBase
    {
        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            //DoWork();
        }
        public void Run()
        {
            PipeServer.RunPipe();
        }
        private void DoWork()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                EventLog.WriteEntry("Service1", $"Running as: {identity.Name}", EventLogEntryType.Information);
                
                if (identity.IsSystem)
                {
                    // Method 1: Enable Software SAS và dùng SendSAS
                    EnableSoftwareSAS();
                    SendSAS_Direct();
                    
                    // Method 2: Backup - dùng WTS API , caiă taò messagebox
                    //SendSAS_WTS();
                }
                else
                {
                    EventLog.WriteEntry("Service1", "Service is not running as SYSTEM", EventLogEntryType.Warning);
                }
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("Service1", $"Error in DoWork: {ex.Message}", EventLogEntryType.Error);
            }
        }

        // Method 1: SendSAS trực tiếp
        [DllImport("sas.dll", SetLastError = true)]
        static extern bool SendSAS(bool AsUser);

        private void SendSAS_Direct()
        {
            try
            {
                bool result = SendSAS(false); // false = as service
                if (result)
                {
                    EventLog.WriteEntry("Service1", "SendSAS called successfully", EventLogEntryType.Information);
                }
                else
                {
                    int error = Marshal.GetLastWin32Error();
                    EventLog.WriteEntry("Service1", $"SendSAS failed with error: {error}", EventLogEntryType.Error);
                }
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("Service1", $"SendSAS_Direct error: {ex.Message}", EventLogEntryType.Error);
            }
        }

        // Method 2: Dùng WinSTA API
        [DllImport("winsta.dll", SetLastError = true)]
        static extern bool WinStationSendMessage(
            IntPtr hServer,
            int SessionId,
            string pTitle,
            int TitleLength,
            string pMessage,
            int MessageLength,
            int Style,
            int Timeout,
            out int pResponse,
            bool bWait);

        [DllImport("kernel32.dll")]
        static extern uint WTSGetActiveConsoleSessionId();

        private void SendSAS_WTS()
        {
            try
            {
                uint sessionId = WTSGetActiveConsoleSessionId();
                EventLog.WriteEntry("Service1", $"Active session: {sessionId}", EventLogEntryType.Information);
                
                if (sessionId != 0xFFFFFFFF)
                {
                    // Gửi SAS command
                    int response;
                    bool result = WinStationSendMessage(
                        IntPtr.Zero,
                        (int)sessionId,
                        "SAS",
                        3,
                        "SAS",
                        3,
                        0,
                        0,
                        out response,
                        false);
                        
                    EventLog.WriteEntry("Service1", $"WinStationSendMessage result: {result}", EventLogEntryType.Information);
                }
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("Service1", $"SendSAS_WTS error: {ex.Message}", EventLogEntryType.Error);
            }
        }

        // Enable Software SAS Generation
        private void EnableSoftwareSAS()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", true))
                {
                    if (key != null)
                    {
                        // 0 = Disable, 1 = Services only, 2 = Services and Ease of Access, 3 = Services and applications
                        key.SetValue("SoftwareSASGeneration", 3, RegistryValueKind.DWord);
                        EventLog.WriteEntry("Service1", "Software SAS generation enabled", EventLogEntryType.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("Service1", $"EnableSoftwareSAS error: {ex.Message}", EventLogEntryType.Error);
            }
        }

        // Method 3: Alternative - dùng Windows API khác
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        private void SendSAS_Alternative()
        {
            try
            {
                // Tìm desktop window
                IntPtr desktop = FindWindow("Progman", null);
                if (desktop != IntPtr.Zero)
                {
                    // Send SAS hotkey message
                    const uint WM_HOTKEY = 0x0312;
                    const int SAS_HOTKEY_ID = 0x0000C000; // Special SAS hotkey ID
                    
                    bool result = SendMessage(desktop, WM_HOTKEY, new IntPtr(SAS_HOTKEY_ID), IntPtr.Zero);
                    EventLog.WriteEntry("Service1", $"Alternative SAS method result: {result}", EventLogEntryType.Information);
                }
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("Service1", $"SendSAS_Alternative error: {ex.Message}", EventLogEntryType.Error);
            }
        }

        protected override void OnStop()
        {
        }
    }
}