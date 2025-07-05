using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
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
        private ManualResetEvent _resetEvent;
        private ClientInfo _clientInfo;
        private RemoteClient _remoteClient;
        public FormMain()
        {
            InitializeComponent();
            Me = Utils.Extensions.InitInfo();
            RemoteClient = new RemoteClient();

            this.Icon = new Icon(@"Resources\logo.ico");
            this.txtOwnerId.Text = Me.Id;
            this.txtOwnerPassword.Text = Me.Password;
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
                    client.LoginEventHandler -= LoginSuccessEvent;
                    client.P2PConnectEventHandler -= P2PConnectEvent;
                }
                _remoteClient = value;
                client = _remoteClient;
                if (client != null)
                {
                    client.ConnectSckEventHandler -= SocketEvent;
                    client.LoginEventHandler += LoginSuccessEvent;
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
                g.FillEllipse(brush, 0, 0, pnStatus.Width - 1, pnStatus.Height - 1);
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
        private void P2PConnectEvent()
        {
            throw new NotImplementedException();
        }

        private void LoginSuccessEvent()
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
        private void Login()
        {
            string data = Utils.Extensions.DataStringBuilder(new string[] { Me.ToString() });
            byte[] dataBytes = Encoding.ASCII.GetBytes(data);
            RemoteClient.Send(Models.Enums.CommandType.Login, dataBytes);
        }
        #endregion
    }
}
