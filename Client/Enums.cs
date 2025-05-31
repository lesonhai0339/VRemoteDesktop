using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemoteClient
{
    public class Enums
    {
        public enum RemoteType: int
        {
            REMOTE = 1,
            CLIENT = 2
        }
        public enum PackageType: int
        {
            CONNCECT = 1,
            DATA = 2
        }
        public enum KeyState : int
        {
            KeyDown = 0,
            KeyUp = 1
        }
        public enum DataSendType: int
        {
            KEYBOARD = 0,
            KEYBOARDRECEIVED = 1,
            MOUSE = 2,
            SCREEN= 3,
            SCREENCHANGE= 4
        }
    }
}
