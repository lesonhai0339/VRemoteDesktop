using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RemoteClient.Remote
{
    public partial class FormRemote :Form
    {
        private SocketRemoteClient _client;
        private ConnectionInfo _connectionInfo;
        public FormRemote(SocketRemoteClient client, ConnectionInfo remoteData)
        {
            InitializeComponent();
            Client = client;
            _connectionInfo = remoteData;
            Text = _connectionInfo.PartnerInfo.Id.Trim();
            //Icon = new Icon("Resources/logo.ico");
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ClientSize = new Size(remoteData.PartnerInfo.Width, remoteData.PartnerInfo.Height);

            // Create and configure PictureBox
            vPictureBox1.Size = new Size(remoteData.PartnerInfo.Width, remoteData.PartnerInfo.Height);
            vPictureBox1.Location = new Point(0, 0);
            vPictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;

            KeyDown += KeyDownEventHandler;
            KeyUp += KeyUpEventHandler;
            MouseMove += MouseMoveEventHandler;
            MouseClick += MouseClickEventHandler;
            MouseWheel += MouseWheelEventHandler;
        }
        #region Properties
        public SocketRemoteClient Client
        {
            get => _client;
            set
            {
                if (_client != null)
                {
                    _client.SendScreenEventHandler -= ScreenEvent;
                }
                _client = value;
                if (_client != null)
                {
                    _client.SendScreenEventHandler += ScreenEvent;
                }
            }
        }
        #endregion
        private void FormRemote_Load(object sender, EventArgs e)
        {
        }
        public void ScreenEvent(byte[] data)
        {
            Console.WriteLine(BitConverter.ToString(data));
            Bitmap image;
            using (MemoryStream stream = new MemoryStream(data))
            {
                image = new Bitmap(stream);
            }
            vPictureBox1.Image = image;
        }
        public void KeyDownEventHandler(object sender, KeyEventArgs e)
        {
            Console.WriteLine($"Down: {e.KeyCode} - {e.Modifiers}");
        }
        public void KeyUpEventHandler(object sender, KeyEventArgs e)
        {
            Console.WriteLine($"Up: {e.KeyCode} - {e.Modifiers}");
        }
        public void MouseMoveEventHandler(object sender, MouseEventArgs e)
        {
            Console.WriteLine($"Mouse move: X:{e.X} - Y:{e.Y}");
        }
        public void MouseClickEventHandler(object sender, MouseEventArgs e)
        {
            Console.WriteLine($"Mouse Click: X:{e.Delta} - Y:{e.Y}");
        }
        public void MouseWheelEventHandler(object sender, MouseEventArgs e)
        {
            Console.WriteLine($"Mouse Wheel: {e.Location}");
        }

        private void vPictureBox1_Click(object sender, EventArgs e)
        {

        }
        //protected override void WndProc(ref Message m)
        //{
        //    Console.WriteLine(m.Msg);
        //    base.WndProc(ref m);
        //}
    }
}
