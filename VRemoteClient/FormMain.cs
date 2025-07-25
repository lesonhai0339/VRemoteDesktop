using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VRemoteClient.Models.Entities;
using VRemoteClient.Services;
using static VRemoteClient.Services.RemoteClient;

namespace VRemoteClient
{
    public partial class FormMain : Form
    {
        private object _lockObject = new object();
        private bool _isSocketConnected;
        private ManualResetEvent _resetEvent;
        private ConnectionInfo _connectionInfo;
        private RemoteDesktopService _remoteDesktop;
        public FormMain()
        {
            InitializeComponent();
            RemoteDesktop = new RemoteDesktopService();
            _isSocketConnected = false;
            _resetEvent = new ManualResetEvent(false);
            this.Text = "VRemote - Vinhhy";
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "logo.ico");
            this.Icon = new Icon(iconPath);
            this.MaximizeBox = false;
            this.txtOwnerId.Text = RemoteDesktop.OwnerInfo.Id;
            this.txtOwnerPassword.Text = RemoteDesktop.OwnerInfo.Password;
            txtPartnerPassword.UseSystemPasswordChar = true;
        }

        #region Properties
        public RemoteDesktopService RemoteDesktop
        {
            get
            {
                lock (_lockObject)
                {
                    return _remoteDesktop;
                }
            }
            set
            {
                lock (_lockObject)
                {
                    if (_remoteDesktop != null)
                    {
                        RemoteDesktop.LoginEvent -= LoginCallback;
                    }
                    _remoteDesktop = value;
                    if (_remoteDesktop != null)
                    {
                        RemoteDesktop.LoginEvent += LoginCallback;
                    }
                }
            }
        }
        #endregion
        #region Methods
        private void FormMain_Load(object sender, EventArgs e)
        {

        }
        private void FormMain_Shown(object sender, EventArgs e)
        {
            ConnectToServer();
        }
        private void pnStatus_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Color circleColor = RemoteDesktop.IsSocketConnected ? Color.Green : Color.Red;
            using (SolidBrush brush = new SolidBrush(circleColor))
            {
                g.FillEllipse(brush, 1, 1, pnStatus.Width - 2, pnStatus.Height - 2);
            }
        }
        private void ConnectToServer()
        {
            string serverIp = ConfigurationManager.AppSettings["RemoteServerIP"];
            string serverPort = ConfigurationManager.AppSettings["RemoteServerPort"];
            var address = IPAddress.Parse(serverIp);
            IPEndPoint remoteEP = new IPEndPoint(address, int.Parse(serverPort));
            RemoteDesktop.ConnectToServer(serverIp, int.Parse(serverPort));
        }
        private void P2PConnectEvent(bool flag, ConnectionInfo? info)
        {
            if (!flag)
            {
                _resetEvent.Set();
            }
            else
            {
                if(info != null)
                {
                    _resetEvent.Set();
                    _connectionInfo = info;
                }
            }
        }

        private void LoginCallback(bool flag)
        {
            if (flag)
            {
                _isSocketConnected = true;
                if (lbStatus.InvokeRequired)
                {
                    lbStatus.Invoke(new Action(() =>
                    {
                        lbStatus.Text = "Sẵn sàng";
                        pnStatus.Invalidate();
                    }));
                }
                else
                {
                    lbStatus.Text = "Sẵn sàng";
                    pnStatus.Invalidate();
                }
            }
            else
            {
                MessageBox.Show("Đăng nhập thất bại", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            } 
        }
        #endregion
        private void btnConnect_Click(object sender, EventArgs e)
        {
            //if (string.IsNullOrEmpty(txtPartnerId.Text) || string.IsNullOrEmpty(txtPartnerPassword.Text))
            //{
            //    MessageBox.Show("Vui lòng nhập ID và mật khẩu của người dùng cần kết nối.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            //    return;
            //}
            //string receiverInfo = Utils.Extensions.DataStringBuilder(new string[] { Me.Id, txtPartnerId.Text.Trim(), txtPartnerPassword.Text.Trim() });
            //byte[] dataBytes = Encoding.ASCII.GetBytes(receiverInfo);
            //RemoteClient.AddWork(new TaskObject
            //(
            //    taskType:  Models.Enums.RemoteType.P2PConnect, 
            //    data: dataBytes
                
            //));
            //bool flag = _resetEvent.WaitOne(1000 * 5);
            //if (flag)
            //{
            //    ConnectionInfo info = new ConnectionInfo() 
            //    {
            //        SessionId = _connectionInfo.SessionId,
            //        Receiver = _connectionInfo.Receiver,
            //        Sender = _connectionInfo.Sender
            //    };
            //    FormRemote frmRemote = new FormRemote(RemoteClient, info, GlobalKeyboardHook);
            //    frmRemote.Show();
            //}
            //else
            //{
            //    MessageBox.Show("Kết nối P2P thất bại. Vui lòng kiểm tra lại ID và mật khẩu của người dùng cần kết nối.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //}
            //_connectionInfo = null;
            //_resetEvent.Reset();
        }
    }
}
