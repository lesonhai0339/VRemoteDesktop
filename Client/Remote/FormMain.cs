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

namespace RemoteClient.Remote
{
    public partial class FormMain : Form
    {
        private TCPClient _client;
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
            Client = new TCPClient(Enums.RemoteType.UNKNOW);
            this.Text = "Remote";
            this.txtYourId.Text = Me.MyId;
            this.txtYourPwd.Text = Me.MyPwd;
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
        public TCPClient Client
        {
            get => _client;
            set
            {
                _client = value;
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
            
        }
        private void button1_Click(object sender, EventArgs e)
        {
            if(string.IsNullOrEmpty(txtPartnerId.Text) || string.IsNullOrEmpty(txtPartnerPwd.Text))
            {
                MessageBox.Show("Id và Password không được bỏ trống", "Xảy ra lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            FormRemote remote = new FormRemote(Client, null);
            remote.Show();
        }
    }
}
