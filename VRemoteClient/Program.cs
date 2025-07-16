using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using VRemoteClient.Utils;

namespace VRemoteClient
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // normally windows will get scaled logical dimensions(incorrect). need to turn on DPI-Aware to get physical screen dimensions
            // declare DPI aware to access the screen resolution. have been set in app.manifest. see https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setprocessdpiaware
            //Libraries.SetProcessDPIAware();
            Logger.Config();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FormRemote(null, null));
        }
    }
}
