using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.ViewModels;
using VRemoteServer.Models;

namespace VRemoteDesktop.Views
{
    public partial class FormRemote : Form
    {
        private ClientInfo _client;
        private RemoteViewModel _remoteViewModel;
        public FormRemote(ClientInfo client)
        {
            InitializeComponent();
            Client = client;
            RemoteViewModel = new RemoteViewModel(Client);

        }
        #region Properties
        public ClientInfo Client
        {
            get => _client;
            set => _client = value;
        }
        public RemoteViewModel RemoteViewModel
        {
            get
            {
                return _remoteViewModel;
            }
            set
            {
                _remoteViewModel = value;
            }
        }
        #endregion
        private void FormRemote_Load(object sender, EventArgs e)
        {

        }
    }
}
