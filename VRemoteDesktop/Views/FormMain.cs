using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using VRemoteDesktop.Events;
using VRemoteDesktop.Services.ConnectionManager;
using VRemoteDesktop.Services.RemoteDesktop;
using VRemoteDesktop.Services.SystemService;
using VRemoteDesktop.Services.VTCPClient;
using VRemoteDesktop.ViewModels;
using VRemoteDesktop.Views;
using VRemoteServer.Models;

namespace VRemoteDesktop
{
    public partial class FormMain : Form
    {
        private readonly object _lock = new object();
        private MainViewModel _viewModel;
        private FormChat chatForm;
        private bool isShow;

        private readonly RemoteDesktopService _remoteDesktopService;
        public FormMain(RemoteDesktopService remoteDesktopService)
        {
            InitializeComponent();
            _remoteDesktopService = remoteDesktopService;
            ViewModel = new MainViewModel(_remoteDesktopService);
            SetupBinding();
            RegisterChatForm();
            isShow = false;
            this.FormBorderStyle = FormBorderStyle.Fixed3D;

        }
        #region Properties
        public MainViewModel ViewModel
        {
            get
            {
                lock (_lock)
                {
                    return _viewModel;
                }
            }
            set
            {
                lock (_lock)
                {
                    if(_viewModel != null)
                    {
                        _viewModel.ClientAcceptRequestRemote -= ClientAcceptRequestRemoteEventHandler;
                    }
                    _viewModel = value;
                    if(_viewModel != null)
                    {
                        _viewModel.ClientAcceptRequestRemote += ClientAcceptRequestRemoteEventHandler;
                    }
                }
            }
        }


        #endregion
        private void RegisterChatForm()
        {
            chatForm = new FormChat();
            chatForm.FormClosed += ChatForm_ClosedEventHandler;
        }
        private void SetupBinding()
        {
            txtOwnerId.DataBindings.Add("Text", ViewModel, "MyId",
                false, DataSourceUpdateMode.OnPropertyChanged);
            txtOwnerPassword.DataBindings.Add("Text", ViewModel, "MyPassword",
               false, DataSourceUpdateMode.OnPropertyChanged);

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if(e.PropertyName == "IsConnected")
            {
               UpdateConnectionStatus();    
            }
        }
        private void pnStatus_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Color circleColor = ViewModel.IsConnected ? Color.Green : Color.Red;
            using (SolidBrush brush = new SolidBrush(circleColor))
            {
                g.FillEllipse(brush, 1, 1, pnStatus.Width - 2, pnStatus.Height - 2);
            }
        }

        private void FormMain_Load(object sender, EventArgs e)
        {

        }
        private void FormMain_Shown(object sender, EventArgs e)
        {
           Connect();
        }
        private void ChatForm_ClosedEventHandler(object sender, FormClosedEventArgs e)
        {
            chatForm.FormClosed -= ChatForm_ClosedEventHandler;
            chatForm = null;
            isShow = false;
        }
        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!chatForm.IsDisposed)
                chatForm.Close();

            if(_viewModel != null)
                _viewModel.ClientAcceptRequestRemote -= ClientAcceptRequestRemoteEventHandler;
            _viewModel.Dispose();
        }
        private void btnConnect_Click(object sender, EventArgs e)
        {
            string partnerId = txtPartnerId.Text.Replace(" ", "");
            string partnerPassword = txtPartnerPassword.Text.Replace(" ","");
            if(!string.IsNullOrWhiteSpace(partnerId) && !string.IsNullOrWhiteSpace(partnerPassword))
            {
                P2PConnect(partnerId, partnerPassword);
            }
            else
            {
                MessageBox.Show("Thông tin không hợp lệ", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void Connect()
        {
            ViewModel.Connect();
        }
        private void P2PConnect(string id, string password)
        {
            ViewModel.RequestP2PConnect(id, password);
        }
        private void UpdateConnectionStatus()
        {
            Action action = () =>
            {
                lbStatus.Text = "Sẵn sàng";
                pnStatus.Invalidate();
            };
            if (this.InvokeRequired)
            {
                this.Invoke(action);
            }
            else
            {
                action();
            }
        }
        private void ClientAcceptRequestRemoteEventHandler(object sender ,P2PClientDataReceived e)
        {
            if(sender is VClient vClient)
            {
                if(e.Type == Models.SocketDataType.P2PAcceptConnect)
                {
                    OpenRemoteForm(vClient);
                }
                else if(e.Type == Models.SocketDataType.P2PRequestConnect)
                {
                    AddChat(vClient);
                }
            }
        }
        private void OpenRemoteForm(VClient client)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<VClient>(OpenRemoteForm), client);
                return;
            }
            FormRemote remoteForm = new FormRemote(client, _remoteDesktopService);
            remoteForm.Show();
            AddChat(client);
        }
        private void AddChat(VClient client)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<VClient>(AddChat), client);
                return;
            }
            if (client == null) return;

            if (!isShow)
            {
                if(chatForm == null)
                {
                    RegisterChatForm();
                }
                //need to cal event from ChatForm to this to set isShow = false when Chat form disposed
                chatForm.Show();
                isShow = true;
            }
            chatForm.AddConnection(client.SocketId, client);
        }
    }
}
