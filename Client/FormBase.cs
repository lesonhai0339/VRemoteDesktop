using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Client
{
    public class FormBase: Form
    {
        public FormBase()
        {
            ControlAdded += FormBase_ControlAdded;
            MouseMove += Form_MouseMove;
        }
        private void FormBase_ControlAdded(object sender, ControlEventArgs e)
        {
            AttachMouseMoveHandler(e.Control);
        }

        private void AttachMouseMoveHandler(Control control)
        {
            control.MouseMove += Form_MouseMove;

            // Đệ quy cho con của control này nếu có
            foreach (Control child in control.Controls)
            {
                AttachMouseMoveHandler(child);
            }

            control.ControlAdded += (s, e2) => AttachMouseMoveHandler(e2.Control);
        }
        public virtual void Form_MouseMove(object sender, MouseEventArgs e)
        {
        }
    }
}
