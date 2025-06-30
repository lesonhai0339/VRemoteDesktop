using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RemoteClient.Remote
{
    public partial class FormRemote :Form
    {
        private Bitmap _curScreen;
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
                    _client.SendScreenChunksEventHandler -= ChunkScreen;
                }
                _client = value;
                if (_client != null)
                {
                    _client.SendScreenEventHandler += ScreenEvent;
                    _client.SendScreenChunksEventHandler += ChunkScreen;
                }
            }
        }

        private void ChunkScreen(byte[] data)
        {

            if (this.InvokeRequired)
            {
                this.Invoke(new Action<byte[]>(ChunkScreen), data);
                return;
            }

            try
            {
                int x = BitConverter.ToInt32(data, 0);
                int y = BitConverter.ToInt32(data, 4);
                int width = BitConverter.ToInt32(data, 8);
                int height = BitConverter.ToInt32(data, 12);
                byte[] chunk = new byte[data.Length - 16];
                Buffer.BlockCopy(data, 16, chunk, 0, chunk.Length);

                Rectangle rectangle = new Rectangle(x, y, width, height);

                // Draw the chunk onto the main screen bitmap
                using (MemoryStream ms = new MemoryStream(chunk))
                using (Bitmap jpegBitmap = new Bitmap(ms))
                using (Graphics g = Graphics.FromImage(_curScreen))
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.CompositingQuality = CompositingQuality.HighSpeed;
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.SmoothingMode = SmoothingMode.None;
                    g.DrawImage(jpegBitmap, rectangle);
                }

                // Refresh only the updated region (more efficient)
                vPictureBox1.Invalidate(rectangle);

                // OR if you need to update the entire image:
                // RefreshPictureBox();

            }
            catch (Exception ex)
            {
                Console.WriteLine($"ChunkScreen error: {ex.Message}");
            }
        }
        #endregion
        private void FormRemote_Load(object sender, EventArgs e)
        {
        }
        public void ScreenEvent(byte[] data)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<byte[]>(ScreenEvent), data);
                return;
            }

            // UI thread code
            try
            {
                using (MemoryStream stream = new MemoryStream(data))
                {
                    Bitmap image = (Bitmap)Image.FromStream(stream);

                    // Dispose old image to prevent memory leak
                    var oldImage = vPictureBox1.Image;
                    vPictureBox1.Image = image;
                    oldImage?.Dispose();

                    _curScreen?.Dispose();
                    _curScreen = image;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ScreenEvent error: {ex.Message}");
            }
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
