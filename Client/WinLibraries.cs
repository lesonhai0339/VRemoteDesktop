using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    internal class WinLibraries
    {
        [DllImport("user32.dll" , CharSet = CharSet.Ansi)]
        private static extern short GetKeyState(int int_1);
    }
}
