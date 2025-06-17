using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RemoteClient.Remote
{
    public partial class FormRemote :Form
    {
        private RemoteEventHandler _remoteHandler;
        public FormRemote(TCPClient client= null, RemoteData remoteData= null)
        {
            InitializeComponent();
            remoteData = new RemoteData
            {
                Id = "192.168.1.1",
                RoomId = "11111111",
                ComputerName = "Vsign",
                WindowsWidth = 1920,
                WindowsHeight = 1080
            };
            Text = remoteData.Id.Trim();
            //Icon = new Icon("Resources/logo.ico");

            _remoteHandler = new RemoteEventHandler(client, remoteData);

        }
        //protected override void WndProc(ref Message m)
        //{
        //    Console.WriteLine(m.Msg);
        //    base.WndProc(ref m);
        //}
        private void FormRemote_Load(object sender, EventArgs e)
        {
        }
     

    }
}
