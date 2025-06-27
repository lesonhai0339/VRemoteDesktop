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
    public partial class ShowImage : Form
    {
        private Bitmap _curScreen;
        private System.Threading.Timer _timer;
        public ShowImage(Bitmap data)
        {
            InitializeComponent();
            UpdateImage(this,data);
            _timer = new System.Threading.Timer(ChangeScreen, null, 0, (1000 / 10));
        }
        public void UpdateImage(object sender, Bitmap bitmap)
        {
            _curScreen?.Dispose();
            _curScreen = (Bitmap)bitmap.Clone();
            pictureBox1.Image?.Dispose(); // Dispose old image
            pictureBox1.Image = _curScreen;
            pictureBox1.Width = bitmap.Width;
            pictureBox1.Height = bitmap.Height;

            int border = this.Width - this.ClientSize.Width;
            int title = this.Height - this.ClientSize.Height;

            this.Width = bitmap.Width + border;
            this.Height = bitmap.Height + title;

        }
        private void ShowImage_Load(object sender, EventArgs e)
        {

        }
        private void ChangeScreen(object state)
        {
            var data = CaptureScreen.GetScreen();
            if (data.Any())
            {
                Parallel.ForEach(data, item =>
                {
                    ChunkScreen(item.Rectangle.X, item.Rectangle.Y, item.Rectangle.Width, item.Rectangle.Height, item.Bytes);
                });
            }
        }
        private void ChunkScreen(int x, int y, int width, int height, byte[] data)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<int, int, int,int,byte[]>(ChunkScreen),x,y,width,height, data);
                return;
            }
            if (this.IsDisposed || !this.IsHandleCreated)
                return;
            try
            {
                Rectangle rectangle = new Rectangle(x, y, width, height);

                // Draw the chunk onto the main screen bitmap
                using (MemoryStream ms = new MemoryStream(data))
                using (Bitmap jpegBitmap = new Bitmap(ms))
                using (Graphics g = Graphics.FromImage(_curScreen))
                {
                    g.DrawImage(jpegBitmap, rectangle);
                }

                // Refresh only the updated region (more efficient)
                pictureBox1.Invalidate(rectangle);

                // OR if you need to update the entire image:
                // RefreshPictureBox();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ChunkScreen error: {ex.Message}");
            }
        }
        public EventHandler<Bitmap> UpdateHandler => UpdateImage;
    }
}
