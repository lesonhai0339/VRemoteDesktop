using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Client
{
    public partial class FormRemote : FormBase
    {
        public FormRemote(string remoteId = "21 897 056")
        {
            InitializeComponent();
            Text = remoteId.Trim();
            Icon = new Icon("Resources/logo.ico");


            KeyPreview = true;
            KeyDown += FormRemote_KeyDown;
            KeyUp += FormRemote_KeyUp;

        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0200) // WM_MOUSEMOVE
            {
                int x = (short)(m.LParam.ToInt32() & 0xFFFF);          // Low word
                int y = (short)((m.LParam.ToInt32() >> 16) & 0xFFFF);   // High word
                Console.WriteLine($"Mouse Move - X: {x}, Y: {y}");
            }
            if(m.Msg == 256 || m.Msg == 257)
            {
                Keys key = (Keys)m.WParam.ToInt32();
                Console.WriteLine(key);
            }
            base.WndProc(ref m);
        }
        public override void Form_MouseMove(object sender, MouseEventArgs e)
        {
           // Console.WriteLine($"X:{e.X} - Y:{e.Y}");
        }

        private void FormRemote_KeyUp(object sender, KeyEventArgs e)
        {
            //throw new NotImplementedException();
        }

        private void FormRemote_KeyDown(object sender, KeyEventArgs e)
        {
            //throw new NotImplementedException();
        }

        private void FormRemote_Load(object sender, EventArgs e)
        {

        }
    }
}
