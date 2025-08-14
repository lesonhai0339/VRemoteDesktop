using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using VRemoteDesktop.ViewModels;

namespace VRemoteDesktop
{
    public partial class FormMain : Form
    {
        private readonly object _object = new object();
        private MainViewModel _viewModel;
        private ManualResetEvent _resetEvent;
        public FormMain()
        {
            InitializeComponent();
            ViewModel = new MainViewModel();
            SetupBinding();

        }
        #region Properties
        public MainViewModel ViewModel
        {
            get => _viewModel;
            set
            {
                _viewModel = value;
            }
        }
        #endregion
        private void SetupBinding()
        {
            txtOwnerId.DataBindings.Add("Text", ViewModel, "MyId",
                false, DataSourceUpdateMode.OnPropertyChanged);
            txtOwnerPassword.DataBindings.Add("Text", ViewModel, "MyPassword",
               false, DataSourceUpdateMode.OnPropertyChanged);
            txtPartnerId.DataBindings.Add("Text", ViewModel, "PartnerId",
                false, DataSourceUpdateMode.OnPropertyChanged);
            txtPartnerPassword.DataBindings.Add("Text", ViewModel, "PartnerPassword",
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
            string partnetId = txtPartnerPassword.Text.Replace(" ", "");
            string partnetPassword = txtPartnerPassword.Text.Replace(" ","");
            if(!string.IsNullOrWhiteSpace(partnetId) && int.TryParse(partnetPassword, out int password))
            {
                P2PConnec(partnetId, password);
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
        private void P2PConnec(string id, int password)
        {
            ViewModel.P2PConnect(id, password);
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
    }
}
