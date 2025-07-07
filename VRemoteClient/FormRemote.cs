using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteClient.Models.Entities;
using VRemoteClient.Services;

namespace VRemoteClient
{
    public partial class FormRemote : Form
    {
        private RemoteClient _remoteClient;
        private ConnectionInfo _info;
        public FormRemote(RemoteClient remoteClient, ConnectionInfo info)
        {
            InitializeComponent();
            Client = remoteClient;
            _info = info;
        }
        #region Properties
        public RemoteClient Client
        {
            get => _remoteClient;
            set
            {
                RemoteClient client = _remoteClient;
                if(client != null)
                {

                }
                _remoteClient = value;
                client = _remoteClient;
                if(client != null)
                {

                }
            }
        }
        #endregion
        private void FormRemote_Load(object sender, EventArgs e)
        {

        }
    }
}
