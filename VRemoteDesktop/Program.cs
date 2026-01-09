using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace VRemoteDesktop
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Startup startup = new Startup();
            Console.WriteLine("End");
            return;
            startup.Run();
        }
    }
}
