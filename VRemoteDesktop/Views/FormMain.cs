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
            _viewModel = new MainViewModel();
            SetupBinding();

        }
        private void SetupBinding()
        {
            txtOwnerId.DataBindings.Add("Text", _viewModel, "MyId",
                false, DataSourceUpdateMode.OnPropertyChanged);
            txtOwnerPassword.DataBindings.Add("Text", _viewModel, "MyPassword",
               false, DataSourceUpdateMode.OnPropertyChanged);
            txtPartnerId.DataBindings.Add("Text", _viewModel, "PartnerId",
                false, DataSourceUpdateMode.OnPropertyChanged);
            txtPartnerPassword.DataBindings.Add("Text", _viewModel, "PartnerPassword",
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

            Color circleColor = _viewModel.IsConnected ? Color.Green : Color.Red;
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
            _viewModel.Connect();
        }
        private void Connect()
        {
            _viewModel.Connect();
        }
        
    }
}
