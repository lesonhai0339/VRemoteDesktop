using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteClient.Models.Enums
{
    public enum WindowsKeyboardEvent: int
    {
        WH_KEYBOARD_LL = 13,
        WM_KEYDOWN = 0x0100,
        WM_KEYUP = 0x0101,
        VK_LCONTROL = 0xA2,  // Left Control
        VK_RCONTROL = 0xA3,  // Right Control
        VK_SHIFT = 0x10,
        VK_MENU = 0x12, // Alt
        VK_LMENU = 0xA4, // left Alt
        VK_RMENU = 0xA5, // right Alt
    }
}
