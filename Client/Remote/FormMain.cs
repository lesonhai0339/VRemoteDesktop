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
        private System.Threading.Timer _pingTimer;
        private SocketRemoteClient _client;
        private MyData _myData;
        private ManualResetEvent _resetEvent;
        public FormMain()
        {
            InitializeComponent();

            ResetEvent = new ManualResetEvent(false);
            Me = new MyData
            {
                MyId =Utils.RandomStringNumber(8),
                MyPwd= Utils.RandomStringNumber(4)
            };
            Client = new SocketRemoteClient();
            this.Text = "Remote";
            this.txtYourId.Text = Me.MyId;
            this.txtYourPwd.Text = Me.MyPwd;
            this.panel1.Paint += Panel1_Paint;
            this.lbConnectStatus.Text = "Chưa kết nối";
        }
        #region Properties
        public MyData Me
        {
            get => _myData;
            private set
            {
                _myData = value;
            }
        }
        public SocketRemoteClient Client
        {
            get => _client;
            set
            {   if(_client != null)
                {
                    _client.ConnectedEventHandler -= SocketConnected;
                }
                _client = value;
                if(_client != null)
                {
                    _client.ConnectedEventHandler += SocketConnected;
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
            byte[] buffer = new byte[1024];
            buffer[0] = (int)PackageType.CONNCECT;
            buffer[1] = (byte)(int)RemoteType.REMOTE;
            string sessionId = "11111111";
            byte[] sessionIdBytes = Encoding.ASCII.GetBytes(sessionId);
            Buffer.BlockCopy(sessionIdBytes, 0, buffer, 2, sessionIdBytes.Length);
            byte[] data = buffer;

            //Client.Send(data);
            FormRemote remote = new FormRemote(Client, null);
            remote.Show();
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
            string os = Utils.GetScreen().ToString();
            string data = Utils.DataStringBuilder(new string[] {os , Me.MyId, Me.MyPwd });
            byte[] dataBytes = Encoding.ASCII.GetBytes(data);
            Client.Send( Enums.DataType.LOGIN ,dataBytes);
            _pingTimer = new System.Threading.Timer(PingServer, null, 0, 10000);
        }
        private void PingServer(object state)
        {
            Client.Send( Enums.DataType.PING ,new byte[] {(int)Enums.DataType.PING });
        }
        #endregion
    }
}
