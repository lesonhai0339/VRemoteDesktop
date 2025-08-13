using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.ViewModels;

namespace VRemoteDesktop
{
    public partial class FormMain : Form
    {
        private MainViewModel _viewModel;
        public FormMain()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            
        }

        private void FormMain_Load(object sender, EventArgs e)
        {

        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            _viewModel.Connect();
        }
    }
}
