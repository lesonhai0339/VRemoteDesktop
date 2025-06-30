using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RemoteClient.Remote
{
    public partial class ShowImage : Form
    {
        private Bitmap _curScreen;
        private Graphics _screenGraphics;
        private readonly object _screenLock = new object();
        private System.Threading.Timer _timer;
        public ShowImage(Bitmap data)
        {
            InitializeComponent();
            Capture();
            _timer = new System.Threading.Timer(ChunksCapture, null, 0, (1000 / 5));
        }
        private void ShowImage_Load(object sender, EventArgs e)
        {

        }
        private void Capture()
        {
            var bounds = Screen.PrimaryScreen.Bounds;
            Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            }
            using(MemoryStream stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Jpeg);
                byte[] data = stream.ToArray();
                Console.WriteLine(data.Length);
                var dataCompressed = Utils.Compress(data);
                Console.WriteLine(dataCompressed.Length);
            }

            ScreenEvent(bitmap);
        }
        public void ScreenEvent(Bitmap bitmap)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<Bitmap>(ScreenEvent), bitmap);
                return;
            }

            // UI thread code
            try
            {
                if (_curScreen == null)
                {
                    // Set PictureBox size to match bitmap
                    pictureBox1.Width = bitmap.Width;
                    pictureBox1.Height = bitmap.Height;

                    // Set Form size to accommodate PictureBox (including borders/title bar)
                    this.ClientSize = new Size(bitmap.Width, bitmap.Height);

                    // Optional: Prevent resizing
                    this.FormBorderStyle = FormBorderStyle.FixedSingle;
                    this.MaximizeBox = false;
                    this.MinimizeBox = false;
                }

                lock (_screenLock)
                {
                    Bitmap image = bitmap;

                    // Dispose old image to prevent memory leak
                    var oldImage = pictureBox1.Image;
                    _screenGraphics?.Dispose();
                    _curScreen?.Dispose();

                    _curScreen = new Bitmap(image);
                    _screenGraphics = Graphics.FromImage(_curScreen);

                    InitializeGraphicsSettings();

                    pictureBox1.Image = _curScreen;


                    oldImage?.Dispose();
                    image?.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ScreenEvent error: {ex.Message}");
            }
        }

        private void InitializeGraphicsSettings()
        {
            //config graphics
            if (_screenGraphics != null)
            {
                _screenGraphics.CompositingMode = CompositingMode.SourceCopy;
                _screenGraphics.CompositingQuality = CompositingQuality.HighSpeed;
                _screenGraphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                _screenGraphics.SmoothingMode = SmoothingMode.None;
                _screenGraphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;
            }
        }
        private void ChunksCapture(object state)
        {
            var chunks = CaptureScreen.GetScreen();
            foreach (var chunk in chunks)
            {
                ChunkScreen(chunk);
            }
        }
        private void ChunkScreen(CaptureCell cell)
        {
            try
            {
                int x = cell.Rectangle.X;
                int y = cell.Rectangle.Y;
                int width = cell.Rectangle.Width;
                int height = cell.Rectangle.Height;


                Rectangle rectangle = new Rectangle(x, y, width, height);

                // Draw the chunk onto the main screen bitmap
                using (MemoryStream ms = new MemoryStream(cell.Bytes))
                using (Bitmap jpegBitmap = new Bitmap(ms))
                {
                    lock (_screenLock)
                    {
                        if (_curScreen != null && _screenGraphics != null)
                        {
                            _screenGraphics.DrawImage(jpegBitmap, rectangle);
                        }
                        if (this.InvokeRequired)
                        {
                            this.BeginInvoke(new Action<Rectangle>(InvalidateRegion), rectangle);
                        }
                        else
                        {
                            InvalidateRegion(rectangle);
                        }
                    }
                }

                // Refresh only the updated region (more efficient)
                pictureBox1.Invalidate(rectangle);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"ChunkScreen error: {ex.Message}");
            }
        }

        private void InvalidateRegion(Rectangle rectangle)
        {
            pictureBox1.Invalidate(rectangle);
        }

        //private void ChangeScreen(object state)
        //{
        //    var data = CaptureScreen.GetScreen();
        //    if (data.Any())
        //    {
        //        Parallel.ForEach(data, item =>
        //        {
        //            ChunkScreen(item.Rectangle.X, item.Rectangle.Y, item.Rectangle.Width, item.Rectangle.Height, item.Bytes);
        //        });
        //    }
        //}
        //private void ChunkScreen(int x, int y, int width, int height, byte[] data)
        //{
        //    if (this.InvokeRequired)
        //    {
        //        this.Invoke(new Action<int, int, int,int,byte[]>(ChunkScreen),x,y,width,height, data);
        //        return;
        //    }
        //    if (this.IsDisposed || !this.IsHandleCreated)
        //        return;
        //    try
        //    {
        //        Rectangle rectangle = new Rectangle(x, y, width, height);

        //        // Draw the chunk onto the main screen bitmap
        //        using (MemoryStream ms = new MemoryStream(data))
        //        using (Bitmap jpegBitmap = new Bitmap(ms))
        //        using (Graphics g = Graphics.FromImage(_curScreen))
        //        {
        //            g.DrawImage(jpegBitmap, rectangle);
        //        }

        //        // Refresh only the updated region (more efficient)
        //        pictureBox1.Invalidate(rectangle);

        //        // OR if you need to update the entire image:
        //        // RefreshPictureBox();
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"ChunkScreen error: {ex.Message}");
        //    }
        //}
        //public EventHandler<Bitmap> UpdateHandler => UpdateImage;
    }
}
