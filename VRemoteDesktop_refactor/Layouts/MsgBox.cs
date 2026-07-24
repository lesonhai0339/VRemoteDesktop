using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Vsign4.VRemoteDesktop.Layouts
{
    static class MsgBox
    {
        public static DialogResult Show(string message, string caption, MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.Information)
        {
            using (Form frm = new Form())
            {
                frm.TopMost = true;
                frm.StartPosition = FormStartPosition.Manual;
                frm.Location = new Point(-2000, -2000);
                frm.Size = new Size(1, 1);
                frm.ShowInTaskbar = false;
                frm.Show();

                return MessageBox.Show(frm, message, caption, buttons, icon);
            }
        }
    }
}
