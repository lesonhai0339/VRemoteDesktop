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
        private ClientInfo _clientInfo;
        private RemoteClient _remoteClient;
        private ConnectionInfo _connectionInfo;
        private GlobalKeyboardHook _globalKeyboardHook;
        public FormMain()
        {
            InitializeComponent();

            _isSocketConnected = false;
            _resetEvent = new ManualResetEvent(false);
            Me = Utils.Extensions.InitInfo();
            RemoteClient = new RemoteClient(Me);
            this.Text = "VRemote - Vinhhy";
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "logo.ico");
            this.Icon = new Icon(iconPath);
            this.MaximizeBox = false;
            this.txtOwnerId.Text = Me.Id;
            this.txtOwnerPassword.Text = Me.Password;
            txtPartnerPassword.UseSystemPasswordChar = true;
            GlobalKeyboardHook = new GlobalKeyboardHook();
            GlobalKeyboardHook.Start((uint)Process.GetCurrentProcess().Id);
        }

        #region Properties
        public GlobalKeyboardHook GlobalKeyboardHook
        {
            get
            {
                lock (_lockObject)
                {
                    return _globalKeyboardHook;
                }
            }
            set
            {
                lock (_lockObject)
                {
                    _globalKeyboardHook = value;
                }
            }
        }
        public ClientInfo Me
        {
            get => _clientInfo;
            private set
            {
                _clientInfo = value;
            }
        }
        public RemoteClient RemoteClient
        {
            get => _remoteClient;
            set
            {
                RemoteClient client= _remoteClient;
                if(client != null)
                {
                    client.ConnectSckEventHandler -= SocketEvent;
                    client.LoginEventHandler -= LoginCallback;
                    client.P2PConnectEventHandler -= P2PConnectEvent;
                }
                _remoteClient = value;
                client = _remoteClient;
                if (client != null)
                {
                    client.ConnectSckEventHandler += SocketEvent;
                    client.LoginEventHandler += LoginCallback;
                    client.P2PConnectEventHandler += P2PConnectEvent;
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

            Color circleColor = RemoteClient.SocketConnected ? Color.Green : Color.Red;
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
            RemoteClient.Connect(serverIp, int.Parse(serverPort));
        }
        private void SocketEvent()
        {
            Login();
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
        private void Login()
        {
            string data = Utils.Extensions.DataStringBuilder(new string[] { Me.ToString() });
            byte[] dataBytes = Encoding.ASCII.GetBytes(data);
            RemoteClient.AddWork(new TaskObject
            (
                taskType: Models.Enums.CommandType.Login,
                data: dataBytes
            ));
        }
        #endregion
        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtPartnerId.Text) || string.IsNullOrEmpty(txtPartnerPassword.Text))
            {
                MessageBox.Show("Vui lòng nhập ID và mật khẩu của người dùng cần kết nối.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string receiverInfo = Utils.Extensions.DataStringBuilder(new string[] { Me.Id, txtPartnerId.Text.Trim(), txtPartnerPassword.Text.Trim() });
            byte[] dataBytes = Encoding.ASCII.GetBytes(receiverInfo);
            RemoteClient.AddWork(new TaskObject
            (
                taskType:  Models.Enums.CommandType.P2PConnect, 
                data: dataBytes
                
            ));
            bool flag = _resetEvent.WaitOne(1000 * 5);
            if (flag)
            {
                ConnectionInfo info = new ConnectionInfo() 
                {
                    SessionId = _connectionInfo.SessionId,
                    Receiver = _connectionInfo.Receiver,
                    Sender = _connectionInfo.Sender
                };
                FormRemote frmRemote = new FormRemote(RemoteClient, info, GlobalKeyboardHook);
                frmRemote.Show();
            }
            else
            {
                MessageBox.Show("Kết nối P2P thất bại. Vui lòng kiểm tra lại ID và mật khẩu của người dùng cần kết nối.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            _connectionInfo = null;
            _resetEvent.Reset();
        }
    }
}
