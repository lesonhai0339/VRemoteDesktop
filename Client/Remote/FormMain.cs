using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Configuration;
using System.Net;
using System.Threading;
using static RemoteClient.Enums;

namespace RemoteClient.Remote
{
    public partial class FormMain : Form
    {
        private bool _isP2PConnected;
        private System.Threading.Timer _pingTimer;
        private SocketRemoteClient _client;
        private Info _myInfo;
        private ManualResetEvent _resetEvent;
        public FormMain()
        {
            InitializeComponent();

            ResetEvent = new ManualResetEvent(false);
            Me = Utils.InitInfo();
            Client = new SocketRemoteClient();
            this.Text = "Remote";
            this.txtYourId.Text = Me.Id;
            this.txtYourPwd.Text = Me.Password;
            this.panel1.Paint += Panel1_Paint;
            this.lbConnectStatus.Text = "Chưa kết nối";
            _isP2PConnected = false;
        }
        #region Properties
        public Info Me
        {
            get => _myInfo;
            private set
            {
                _myInfo = value;
            }
        }
        public SocketRemoteClient Client
        {
            get => _client;
            set
            {   if(_client != null)
                {
                    _client.ConnectedEventHandler -= SocketConnected;
                    _client.P2PRemoteSuccessEventHandler -= P2PConnected;
                }
                _client = value;
                if(_client != null)
                {
                    _client.ConnectedEventHandler += SocketConnected;
                    _client.P2PRemoteSuccessEventHandler += P2PConnected;

                }
            }
        }

        public ManualResetEvent ResetEvent
        {
            get => _resetEvent;
            private set
            {
                _resetEvent = value;
            }
        }
        #endregion
        #region Functions
        private void Panel1_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Color circleColor = Client.SocketConnected ? Color.Green : Color.Red;
            using (SolidBrush brush = new SolidBrush(circleColor))
            {
                g.FillEllipse(brush, 0, 0, panel1.Width - 1, panel1.Height - 1);
            }
        }
        private void ConnectToServer()
        {
            string serverIp = ConfigurationManager.AppSettings["RemoteServerIP"];
            string serverPort = ConfigurationManager.AppSettings["RemoteServerPort"];
            var address = IPAddress.Parse(serverIp);
            IPEndPoint remoteEP = new IPEndPoint(address, int.Parse(serverPort));
            Client.Connect(remoteEP);
        }
        private void FormMain_Load(object sender, EventArgs e)
        {

        }
        private void FormMain_Shown(object sender, EventArgs e)
        {
            ConnectToServer();
        }
        private void button1_Click(object sender, EventArgs e)
        {
            if(string.IsNullOrEmpty(txtPartnerId.Text) || string.IsNullOrEmpty(txtPartnerPwd.Text))
            {
                MessageBox.Show("Id và Password không được bỏ trống", "Xảy ra lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            string remoteInfo = Utils.DataStringBuilder(new string[] { txtPartnerId.Text.Replace(" ", ""), txtPartnerPwd.Text.Replace(" ","") });
            byte[] dataBytes = Encoding.ASCII.GetBytes(remoteInfo);

            Client.Send(Enums.DataType.P2PCONNECT, dataBytes);
            ResetEvent.WaitOne(1000 * 10);
            ResetEvent.Reset();
            if (_isP2PConnected)
            {
                FormRemote remote = new FormRemote(Client, null);
                remote.Show();
            }
            else
            {
                MessageBox.Show($"Không thể kết nối đến {txtPartnerId.Text}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }
        private void SocketConnected()
        {
            Console.WriteLine("Isconnected");
            if (lbConnectStatus.InvokeRequired)
            {
                lbConnectStatus.Invoke(new Action(() =>
                {
                    lbConnectStatus.Text = "Đã kết nối";
                    panel1.Invalidate();
                }));
            }
            else
            {
                lbConnectStatus.Text = "Đã kết nối";
                panel1.Invalidate();
            }
            Login();
        }
        private void Login()
        {
            string data = Utils.DataStringBuilder(new string[] {Me.ToString()});
            byte[] dataBytes = Encoding.ASCII.GetBytes(data);
            Client.Send( Enums.DataType.LOGIN ,dataBytes);
            _pingTimer = new System.Threading.Timer(PingServer, null, 0, 10000);
        }
        private void PingServer(object state)
        {
            Client.Send( Enums.DataType.PING ,new byte[] {(int)Enums.DataType.PING });
        }
        private void P2PConnected(bool flag)
        {
            _isP2PConnected = flag;
            ResetEvent.Set();
        }

        #endregion
    }
}
