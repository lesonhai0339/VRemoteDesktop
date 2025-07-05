using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VRemoteClient.Models.Entities;

namespace VRemoteClient
{
    public partial class FormMain : Form
    {
        private bool _isSocketConnected;
        private ManualResetEvent _resetEvent;
        private ClientInfo _clientInfo;

        public FormMain()
        {
            InitializeComponent();
            Me = Utils.Extensions.InitInfo(); 
            this.txtOwnerId.Text = Me.Id;
            this.txtOwnerPassword.Text = Me.Password;
            this.Text = "VRemote - Vinhhy";
            this.Icon = new Icon(@"Resources\logo.ico");
            this.pnStatus.Paint += pnStatus_Paint;
        }
        #region Properties
        public ClientInfo Me
        {
            get => _clientInfo;
            private set
            {
                _clientInfo = value;
            }
        }
        #endregion
        private void pnStatus_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Color circleColor = /*Client.SocketConnected*/ true ? Color.Green : Color.Red;
            using (SolidBrush brush = new SolidBrush(circleColor))
            {
                g.FillEllipse(brush, 0, 0, pnStatus.Width - 1, pnStatus.Height - 1);
            }
            UpdateStatus();
        }
        private void UpdateStatus()
        {
            if (lbStatus.InvokeRequired)
            {
                lbStatus.Invoke(new Action(() =>
                {
                    lbStatus.Text = "Đã kết nối";
                    //pnStatus.Invalidate();
                }));
            }
            else
            {
                lbStatus.Text = "Đã kết nối";
                //pnStatus.Invalidate();
            }
        }
        private void FormMain_Load(object sender, EventArgs e)
        {

        }

    }
}
