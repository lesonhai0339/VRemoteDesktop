using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.ServiceProcess;
using System.Text;
using System.Threading;

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
            LaunchViaWMI();
        }

        protected override void OnStop()
        {
        }
        public void LaunchViaWMI()
        {
            try
            {
                // Lấy active session ID
                uint sessionId = WTSGetActiveConsoleSessionId();
                WriteLog($"Active session ID: {sessionId}");

                // Connect to WMI với credentials của user hiện tại
                ManagementScope scope = new ManagementScope($@"\\.\root\cimv2");
                scope.Connect();

                ManagementClass processClass = new ManagementClass(scope, new ManagementPath("Win32_Process"), null);
                ManagementBaseObject inParams = processClass.GetMethodParameters("Create");

                inParams["CommandLine"] = @"C:\Users\admin\source\repos\VRemoteDesktop\VRemoteClient\bin\Debug\net40\VRemoteClient.exe";
                inParams["CurrentDirectory"] = @"C:\Users\admin\source\repos\VRemoteDesktop\VRemoteClient\bin\Debug\net40";

                // Quan trọng: Set ProcessStartupInformation để specify session
                ManagementBaseObject startupInfo = processClass.GetMethodParameters("Create").Properties["ProcessStartupInformation"].Value as ManagementBaseObject;
                if (startupInfo == null)
                {
                    startupInfo = new ManagementClass(scope, new ManagementPath("Win32_ProcessStartup"), null).CreateInstance();
                }

                startupInfo["ShowWindow"] = 1; // SW_NORMAL
                startupInfo["Title"] = "VRemoteClient";

                inParams["ProcessStartupInformation"] = startupInfo;

                ManagementBaseObject outParams = processClass.InvokeMethod("Create", inParams, null);

                uint returnValue = (uint)outParams["returnValue"];
                if (returnValue == 0)
                {
                    uint processId = (uint)outParams["processId"];
                    WriteLog($"WMI Launch successful. ProcessId: {processId}");

                    // Kiểm tra process có chạy không
                    Thread.Sleep(2000);
                    try
                    {
                        Process proc = Process.GetProcessById((int)processId);
                        WriteLog($"Process {proc.ProcessName} is running in session {proc.SessionId}");
                    }
                    catch
                    {
                        WriteLog("Process not found or exited");
                    }
                }
                else
                {
                    WriteLog($"WMI Launch failed: {returnValue}");
                }
            }
            catch (Exception ex)
            {
                WriteLog($"WMI error: {ex.Message}");
            }
        }
        public void StartWinFormsInUserSession()
        {
            try
            {
                EnablePrivilege("SeIncreaseQuotaPrivilege");
                EnablePrivilege("SeAssignPrimaryTokenPrivilege");
                EnablePrivilege("SeTcbPrivilege");
                WriteLog("1");

                uint sessionId = WTSGetActiveConsoleSessionId();
                if (WTSQueryUserToken(sessionId, out IntPtr userToken))
                {

                    WriteLog("2");

                    SECURITY_ATTRIBUTES sa = new SECURITY_ATTRIBUTES();
                    sa.nLength = Marshal.SizeOf(sa);

                    if (!DuplicateTokenEx(
                       userToken,
                       TOKEN_ALL_ACCESS,
                       ref sa,
                       SecurityImpersonation,
                       TokenPrimary,
                       out IntPtr duplicatedToken))
                    {
                        int err = Marshal.GetLastWin32Error();
                        WriteLog("3 "+ err);

                        return;
                    }

                    uint activeSessionId = WTSGetActiveConsoleSessionId();
                    bool setSession = SetTokenInformation(
                        duplicatedToken,
                        TokenSessionId,
                        ref activeSessionId,
                        (uint)Marshal.SizeOf(typeof(uint))
                    );

                    if (!setSession)
                    {
                        int err = Marshal.GetLastWin32Error();
                        WriteLog("SetTokenInformation failed: " + err);
                        return;
                    }

                    WriteLog("4");

                    STARTUPINFO si = new STARTUPINFO();
                    si.cb = (uint)Marshal.SizeOf(si);
                    si.lpDesktop = "winsta0\\default";  // bắt buộc để hiện GUI
                    si.dwFlags = STARTF_USESHOWWINDOW;
                    si.wShowWindow = SW_SHOW; // hoặc SW_NORMAL
                    PROCESS_INFORMATION pi = new PROCESS_INFORMATION();

                    string appPath = @"C:\Windows\System32\notepad.exe";
                    WriteLog("4.5");

                    bool envResult = CreateEnvironmentBlock(out IntPtr envBlock, duplicatedToken, false);
                    if (!envResult)
                    {
                        WriteLog("CreateEnvironmentBlock failed");
                        CloseHandle(duplicatedToken);
                        CloseHandle(userToken);
                        return;
                    }
                    bool result = CreateProcessAsUser(
                           duplicatedToken,
                           null,
                           appPath,
                           ref sa,
                           ref sa,
                           false,
                           CREATE_NEW_CONSOLE | CREATE_UNICODE_ENVIRONMENT,
                           envBlock, // <-- Dùng environment block
                           null,
                           ref si,
                           out pi
                       );
                    DestroyEnvironmentBlock(envBlock);
                    WriteLog("5");
                    if (result)
                    {
                        WriteLog($"Process created successfully. ProcessId: {pi.dwProcessId}");

                        // Đợi process kết thúc và lấy exit code
                        WaitForSingleObject(pi.hProcess, 5000); // Đợi 5 giây

                        if (GetExitCodeProcess(pi.hProcess, out uint exitCode))
                        {
                            WriteLog($"Process exit code: {exitCode}");
                            if (exitCode == STILL_ACTIVE)
                                WriteLog("Process still running");
                            else
                                WriteLog($"Process exited with code: {exitCode}");
                        }
                    }
                    else
                    {
                        int error = Marshal.GetLastWin32Error();
                        WriteLog($"CreateProcessAsUser failed: {error}");
                    }
                    CloseHandle(duplicatedToken);
                    CloseHandle(userToken);
                    WriteLog($"Cleaned");

                }
                if (!WTSQueryUserToken(sessionId, out userToken))
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    WriteLog("WTSQueryUserToken failed. Error code: " + errorCode);
                }
            }

            catch(Exception ex)
            {
                WriteLog("WTSQueryUserToken x failed. Error code: " + ex.Message);
            }
        }
        [DllImport("kernel32.dll")]
        static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll")]
        static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

        const uint STILL_ACTIVE = 259;
        [DllImport("user32.dll")]
        static extern uint WaitForInputIdle(IntPtr hProcess, uint dwMilliseconds);
        const uint STARTF_USESHOWWINDOW = 0x00000001;
        const ushort SW_SHOW = 5;
        const ushort SW_NORMAL = 1;
        const uint CREATE_NEW_CONSOLE = 0x00000010;
        const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
        [DllImport("userenv.dll", SetLastError = true)]
        static extern bool CreateEnvironmentBlock(out IntPtr lpEnvironment, IntPtr hToken, bool bInherit);

        [DllImport("userenv.dll", SetLastError = true)]
        static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint WTSGetActiveConsoleSessionId();

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool SetTokenInformation(
            IntPtr TokenHandle,
            int TokenInformationClass,
            ref uint TokenInformation,
            uint TokenInformationLength
        );

        const int TokenSessionId = 12;
        [DllImport("wtsapi32.dll", SetLastError = true)]
        static extern bool WTSQueryUserToken(uint sessionId, out IntPtr Token);


        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool CreateProcessAsUser(
            IntPtr hToken,
        string lpApplicationName,
            string lpCommandLine,
            ref SECURITY_ATTRIBUTES lpProcessAttributes,
            ref SECURITY_ATTRIBUTES lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation
        );
        public void WriteLog(string data)
        {
            using (StreamWriter write = new StreamWriter(@"C:\Users\admin\source\repos\VRemoteDesktop\VRemoteClient\bin\Debug\net40\log.txt", true))
            {
                write.WriteLine(data);
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);
        [StructLayout(LayoutKind.Sequential)]
        struct STARTUPINFO
        {
            public uint cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public uint dwX;
            public uint dwY;
            public uint dwXSize;
            public uint dwYSize;
            public uint dwXCountChars;
            public uint dwYCountChars;
            public uint dwFillAttribute;
            public uint dwFlags;
            public ushort wShowWindow;
            public ushort cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public bool bInheritHandle;
        }
        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, int BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

        [StructLayout(LayoutKind.Sequential)]
        struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public LUID Luid;
            public uint Attributes;
        }

        const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        const uint TOKEN_QUERY = 0x0008;
        const uint SE_PRIVILEGE_ENABLED = 0x00000002;

        public void EnablePrivilege(string privilege)
        {
            if (!OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out IntPtr tokenHandle))
                WriteLog($"OpenProcessToken failed for {privilege}");


            if (!LookupPrivilegeValue(null, privilege, out LUID luid))
                WriteLog($"LookupPrivilegeValue failed for {privilege}");

            TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Luid = luid,
                Attributes = SE_PRIVILEGE_ENABLED
            };

            if (!AdjustTokenPrivileges(tokenHandle, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero))
                WriteLog($"AdjustTokenPrivileges failed for {privilege}");

            // Kiểm tra lỗi cuối cùng
            int lastError = Marshal.GetLastWin32Error();
            if (lastError != 0)
                WriteLog($"EnablePrivilege {privilege} failed with error");
        }
        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool DuplicateTokenEx(
    IntPtr hExistingToken,
    uint dwDesiredAccess,
    ref SECURITY_ATTRIBUTES lpTokenAttributes,
    int ImpersonationLevel,
    int TokenType,
    out IntPtr phNewToken
);

        const uint TOKEN_ALL_ACCESS = 0xF01FF;
        const int SecurityImpersonation = 2;
        const int TokenPrimary = 1;
    }
}
