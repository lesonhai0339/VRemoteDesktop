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
        private SocketRemoteClient _client;
        private ConnectionInfo _connectionInfo;
        public FormRemote(SocketRemoteClient client, ConnectionInfo remoteData)
        {
            InitializeComponent();
            _client = client;
            _connectionInfo = remoteData;
            Text = _connectionInfo.PartnerInfo.Id.Trim();
            //Icon = new Icon("Resources/logo.ico");

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
            private set
            {
                if (_client != value)
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
        private void ScreenEvent(byte[] data)
        {
            throw new NotImplementedException();
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
        //protected override void WndProc(ref Message m)
        //{
        //    Console.WriteLine(m.Msg);
        //    base.WndProc(ref m);
        //}
    }
}
