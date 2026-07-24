using System;
using System.Windows.Forms;

namespace Vsign4.VRemoteDesktop
{
    /// <summary>
    /// Standalone entry point. When this module is copied into the Vsign4 host app it is
    /// launched via <see cref="VSetup"/> from the host; for the standalone VRemoteDesktop.exe
    /// we provide our own Main that runs the same setup.
    /// </summary>
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            new VSetup().Run();
        }
    }
}
