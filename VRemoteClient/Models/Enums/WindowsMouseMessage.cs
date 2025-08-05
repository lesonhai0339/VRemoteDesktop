using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteClient.Models.Enums
{
    [Flags]
    public enum WindowsMouseMessage
    {
        None = 0x0000,
        // Left mouse
        WM_LBUTTONDBLCLK = 0x0203, // Left mouse double click
        WM_LBUTTONDOWN = 0x0201, // Left mouse pressed down
        WM_LBUTTONUP = 0x0202, // Left mouse released
        WM_NCLBUTTONDBLCLK = 0x00A3, // Non-client left mouse double click
        WM_NCLBUTTONDOWN = 0x00A1, // Non-client left mouse pressed down
        WM_NCLBUTTONUP = 0x00A2, // Non-client left mouse released

        // Middle mouse
        WM_MBUTTONDBLCLK = 0x0209, // Middle mouse double click
        WM_MBUTTONDOWN = 0x0207, // Middle mouse pressed down
        WM_MBUTTONUP = 0x0208, // Middle mouse released
        WM_NCMBUTTONDBLCLK = 0x00A9, // Non-client middle mouse double click
        WM_NCMBUTTONDOWN = 0x00A7, // Non-client middle mouse pressed down
        WM_NCMBUTTONUP = 0x00A8, // Non-client middle mouse released
        WM_NCMOUSEHOVER = 0x02A1, // Non-client mouse hover
        WM_NCMOUSELEAVE = 0x02A3, // Non-client mouse leave
        WM_NCMOUSEMOVE = 0x00A0, // Non-client mouse move

        // Right mouse
        WM_RBUTTONDBLCLK = 0x0206, // Right mouse double click
        WM_RBUTTONDOWN = 0x0204, // Right mouse pressed down
        WM_RBUTTONUP = 0x0205, // Right mouse released
        WM_NCRBUTTONDBLCLK = 0x00A6, // Non-client right mouse double click
        WM_NCRBUTTONDOWN = 0x00A4, // Non-client right mouse pressed down
        WM_NCRBUTTONUP = 0x00A5, // Non-client right mouse released

        // All / general
        WM_MOUSEACTIVATE = 0x0021,
        WM_MOUSEHOVER = 0x02A1,
        WM_MOUSEHWHEEL = 0x020E,
        WM_MOUSELEAVE = 0x02A3,
        WM_MOUSEMOVE = 0x0200,
        WM_MOUSEWHEEL = 0x020A,
        WM_NCHITTEST = 0x0084,

        // X mouse (extra buttons)
        WM_XBUTTONDBLCLK = 0x020D, // X mouse double click
        WM_XBUTTONDOWN = 0x020B, // X mouse pressed down
        WM_XBUTTONUP = 0x020C, // X mouse released
        WM_NCXBUTTONDBLCLK = 0x00AB, // Non-client X mouse double click
        WM_NCXBUTTONDOWN = 0x00A9, // Non-client X mouse pressed down
        WM_NCXBUTTONUP = 0x00AA,  // Non-client X mouse released

        //custom for mouse drag and drop
        DRAGDROP_MOUSEDOWN= 0x997,
        DRAGDROP_MOUSEMOVE = 0x998,
        DRAGDROP_MOUSEUP = 0x999,


        //Custom for triple click, only left mouse
        WM_BUTTONTRIPLECLICK = 0x996
    }
}
