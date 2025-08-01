using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteClient.Models.Enums
{
    public class KeyboardEnums
    {
        public enum KeyCombination
        {
            None = 0,
            Copy = 1,
            SAS = 2,
        }
        public enum KeyState : int
        {
            KeyDown = 0,
            KeyUp = 1
        }

    }
}
