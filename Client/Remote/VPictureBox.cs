using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RemoteClient.Remote
{
    public class VPictureBox: PictureBox
    {
        public delegate void ClickEventHandler();

        public delegate void DoubleClickEventHandler();
        public delegate void CtrlShiftClickEventHandler();

        public event ClickEventHandler ClickEventHandler0;
        public event DoubleClickEventHandler DoubleClickEventHandler0;
        public event CtrlShiftClickEventHandler CtrlShiftClickEventHandler0;
        public VPictureBox()
        {
            Click += VPictureBox_Click;
            DoubleClick += VPictureBox_DoubleClick;
            MouseDown += VPictureBox_MouseDown;
            LostFocus += VPictureBox_LostFocus;
            ControlAdded += VPictureBox_ControlAdded;
        }
        private void VPictureBox_Click(object sender, EventArgs e)
        {
            Console.WriteLine("Click");
            ClickEventHandler clickEventHandler = this.ClickEventHandler0;
            if(clickEventHandler != null)
            {
                clickEventHandler();
            }
        }
        private void VPictureBox_DoubleClick(object sender, EventArgs e)
        {
            Console.WriteLine("DBClick");
            DoubleClickEventHandler dbClickEventHandler = this.DoubleClickEventHandler0;
            if (dbClickEventHandler != null)
            {
                dbClickEventHandler();
            }
        }
        private void VPictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            if((ModifierKeys & Keys.Control) != Keys.None && (ModifierKeys & Keys.Shift) != Keys.None)
            {
                CtrlShiftClickEventHandler ctrlShiftClickEventHandler = CtrlShiftClickEventHandler0;
                if(ctrlShiftClickEventHandler != null)
                {
                    ctrlShiftClickEventHandler();
                }
            }
        }
        private void VPictureBox_ControlAdded(object sender, ControlEventArgs e)
        {
        }

        private void VPictureBox_LostFocus(object sender, EventArgs e)
        {
        }
        //public virtual void FormBase_ControlAdded(object sender, ControlEventArgs e)
        //{
        //    AttachMouseMoveHandler(e.Control);
        //}
        //private void AttachMouseMoveHandler(Control control)
        //{
        //    control.MouseMove += _remoteHandler.MouseMoveEventHandler;
        //    control.MouseClick += _remoteHandler.MouseClickEventHandler;

        //    foreach (Control child in control.Controls)
        //    {
        //        AttachMouseMoveHandler(child);
        //    }
        //    control.ControlAdded += (s, e2) => AttachMouseMoveHandler(e2.Control);
        //}
    }
}
