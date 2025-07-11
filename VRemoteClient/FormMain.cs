using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Drawing;
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
        private bool _isSocketConnected;
        private bool _isP2PConnected;   
        private ManualResetEvent _resetEvent;
        private ClientInfo _clientInfo;
        private RemoteClient _remoteClient;
        private ConnectionInfo _connectionInfo;
        private KeyboardHook _keyboardHook;
        private GlobalMouseHook _mouseHook;
        public FormMain()
        {
            InitializeComponent();

            _isSocketConnected = false;
            _isP2PConnected = false;
            _resetEvent = new ManualResetEvent(false);

            Me = Utils.Extensions.InitInfo();
            RemoteClient = new RemoteClient(Me);

            this.Text = "VRemote - Vinhhy";
            this.Icon = new Icon(@"Resources\logo.ico");
            this.txtOwnerId.Text = Me.Id;
            this.txtOwnerPassword.Text = Me.Password;



            _keyboardHook = new KeyboardHook();
            _keyboardHook.KeyPressed += KeyboardEvent;
            _keyboardHook.Start((uint)Process.GetCurrentProcess().Id);
            _mouseHook = new GlobalMouseHook();
            _mouseHook.MouseMove += MouseMoveEvent;
            _mouseHook.MouseClick += MouseClickEvent;
            _mouseHook.StartHook((uint)Process.GetCurrentProcess().Id);
        }

        private void MouseClickEvent(object sender, GlobalMouseHook.MouseEventArgs e)
        {
            Console.WriteLine($"Click detected: {e.Button} {e.Action} at ({e.X}, {e.Y})");
        }

        private void MouseMoveEvent(object sender, GlobalMouseHook.MouseEventArgs e)
        {
            Console.WriteLine($"Move detected: {e.Button} {e.Action} at ({e.X}, {e.Y})");
        }

        private void KeyboardEvent(object sender, KeyMessageEventArgs e)
        {
            Console.WriteLine($"Key Pressed: {e.KeyModifier} - {e.KeyCode} {e.KeyType}");
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
                MessageBox.Show("Kết nối P2P thất bại. Vui lòng kiểm tra lại ID và mật khẩu của người dùng cần kết nối.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                if(info != null)
                {
                    _resetEvent.Set();
                    _isP2PConnected = true;
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
            RemoteClient.Send(Models.Enums.CommandType.Login, dataBytes);
        }
        #endregion

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtPartnerId.Text) || string.IsNullOrEmpty(txtPartnerPassword.Text))
            {
                MessageBox.Show("Vui lòng nhập ID và mật khẩu của người dùng cần kết nối.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            string receiverInfo = Utils.Extensions.DataStringBuilder(new string[] { Me.Id, txtPartnerId.Text.Trim(), txtPartnerPassword.Text.Trim() });
            byte[] dataBytes = Encoding.ASCII.GetBytes(receiverInfo);
            RemoteClient.Send(Models.Enums.CommandType.P2PConnect, dataBytes);
            _resetEvent.WaitOne(1000 * 10);
            _resetEvent.Reset();
            if (_isP2PConnected)
            {
                FormRemote frmRemote = new FormRemote(RemoteClient, _connectionInfo);
                frmRemote.Show();
            }
            else
            {
                MessageBox.Show($"Không thể kết nối đến {txtPartnerId.Text}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            } 
        }
    }
}
