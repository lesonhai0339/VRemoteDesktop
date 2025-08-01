using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.ServiceProcess;
using System.Text;

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
            StartWinFormsInUserSession();
        }

        protected override void OnStop()
        {
        }
        private void StartWinFormsInUserSession()
        {
            uint sessionId = WTSGetActiveConsoleSessionId();
            if (WTSQueryUserToken(sessionId, out IntPtr userToken))
            {
                SECURITY_ATTRIBUTES sa = new SECURITY_ATTRIBUTES();
                sa.nLength = Marshal.SizeOf(sa);

                STARTUPINFO si = new STARTUPINFO();
                si.cb = (uint)Marshal.SizeOf(si);
                si.lpDesktop = "winsta0\\default";  // bắt buộc để hiện GUI

                PROCESS_INFORMATION pi = new PROCESS_INFORMATION();

                string appPath = @"C:\Users\haile\Source\Repos\VRemoteDesktop\VRemoteClient\bin\Debug\net40\VRemoteClient.exe";
                EnablePrivilege("SeIncreaseQuotaPrivilege");
                EnablePrivilege("SeAssignPrimaryTokenPrivilege");
                bool result = CreateProcessAsUser(
                    userToken,
                    null,
                    appPath,
                    ref sa,
                    ref sa,
                    false,
                    0,
                    IntPtr.Zero,
                    null,
                    ref si,
                    out pi
                );

                if (!result)
                {
                    int error = Marshal.GetLastWin32Error();
                    // Log error
                }

                // Dọn tài nguyên
                if (pi.hProcess != IntPtr.Zero) CloseHandle(pi.hProcess);
                if (pi.hThread != IntPtr.Zero) CloseHandle(pi.hThread);
                CloseHandle(userToken);
            }
        }
        [DllImport("wtsapi32.dll", SetLastError = true)]
        static extern bool WTSQueryUserToken(uint sessionId, out IntPtr Token);

        [DllImport("kernel32.dll")]
        static extern uint WTSGetActiveConsoleSessionId();

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

        bool EnablePrivilege(string privilege)
        {
            if (!OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out IntPtr tokenHandle))
                return false;

            if (!LookupPrivilegeValue(null, privilege, out LUID luid))
                return false;

            TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Luid = luid,
                Attributes = SE_PRIVILEGE_ENABLED
            };

            return AdjustTokenPrivileges(tokenHandle, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
        }

    }
}
