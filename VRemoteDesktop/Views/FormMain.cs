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
using VRemoteDesktop.Services.Authentication;
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
        private readonly RemoteDesktopService _remoteDesktopService;
        public FormMain(RemoteDesktopService remoteDesktopService)
        {
            InitializeComponent();
            _remoteDesktopService = remoteDesktopService;
            ViewModel = new MainViewModel(_remoteDesktopService);
            SetupBinding();

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

        private void btnConnect_Click(object sender, EventArgs e)
        {
            string partnetId = txtPartnerId.Text.Replace(" ", "");
            string partnetPassword = txtPartnerPassword.Text.Replace(" ","");
            if(!string.IsNullOrWhiteSpace(partnetId) && !string.IsNullOrWhiteSpace(partnetPassword))
            {
                P2PConnec(partnetId, partnetPassword);
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
        private void P2PConnec(string id, string password)
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
        private void ClientAcceptRequestRemoteEventHandler(object sender ,EventArgs e)
        {
            if(sender is VClient vClient)
            {
                OpenRemoteForm(vClient);

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
        }
    }
}
