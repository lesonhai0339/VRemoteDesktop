using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Markup;
using System.Windows.Media;

namespace RemoteClient.Remote
{
    public partial class FormRemote :Form
    {
        private Bitmap _curScreen;
        private Graphics _screenGraphics;
        private readonly object _screenLock = new object();
        private SocketRemoteClient _client;
        private ConnectionInfo _connectionInfo;

        private List<Rectangle> _chunkRecangles;
        private List<Bitmap> _chunkBitmaps;
        private int _chunkTotalSize;
        private int _curChunksSent;
        public FormRemote(SocketRemoteClient client, ConnectionInfo remoteData)
        {
            InitializeComponent();


            _chunkTotalSize = 0;
            _curChunksSent = 0;
            _chunkRecangles = new List<Rectangle>();
            _chunkBitmaps = new List<Bitmap>();


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
        #endregion
        private void FormRemote_Load(object sender, EventArgs e)
        {
        }
        /* private void ChunkScreen(byte[] data)
         {
             try
             {
                 int totalSize = BitConverter.ToInt32(data, 0);
                 if(_chunkTotalSize != totalSize)
                 {
                     _chunkTotalSize = totalSize;
                 }

                 int x = BitConverter.ToInt32(data, 4);
                 int y = BitConverter.ToInt32(data, 8);
                 int width = BitConverter.ToInt32(data, 12);
                 int height = BitConverter.ToInt32(data, 16);

                 byte[] chunk = new byte[data.Length - 20];
                 Buffer.BlockCopy(data, 20, chunk, 0, chunk.Length);
                 byte[] dataDecompress = Utils.Decompress(chunk);

                 Rectangle rectangle = new Rectangle(x, y, width, height);

                 if (dataDecompress == null || dataDecompress.Length == 0)
                     throw new Exception("Decompressed data is empty or null");

                 using (MemoryStream stream = new MemoryStream(dataDecompress))
                 using (Bitmap bitmap = new Bitmap(stream))
                 {
                     Bitmap bm = bitmap.Clone() as  Bitmap;
                     _chunkBitmaps.Add(bm);
                 }

                 _curChunksSent = _curChunksSent + chunk.Length;
                 _chunkRecangles.Add(rectangle);
                 //update screen if enough data
                 if(_curChunksSent >= _chunkTotalSize)
                 {
                     UpdateScreenByChunks();
                 }

             }
             catch ( Exception ex)
             {
                 Console.WriteLine($"ChunkScreen error: {ex.Message}");
             }
         }
         private void UpdateScreenByChunks()
         {
             try
             {
                 Console.WriteLine($"total chunks size: {_chunkTotalSize}");
                 if(_chunkRecangles.Count != _chunkBitmaps.Count)
                 {
                     Console.WriteLine("Rectangles and bitmaps not same number of packets");
                     throw new Exception("Rectangles and bitmaps not same number of packets");
                 }
                 if(!_chunkRecangles.Any() || !_chunkBitmaps.Any())
                 {
                     Console.WriteLine("Rectangles or bitmaps is empty");
                     throw new Exception("Rectangles or bitmaps is empty");
                 }
                 lock (_screenLock)
                 {
                     if (_curScreen != null && _screenGraphics != null)
                     {
                         _chunkBitmaps.Zip(_chunkRecangles, (bitmap, rectangle) =>
                         new
                         {
                             bitmap,
                             rectangle
                         }).Where(pair =>
                         {
                             _screenGraphics.DrawImage(pair.bitmap, pair.rectangle);
                             return true;
                         }).ToList();

                         if (_chunkRecangles.Count > 0)
                         {
                             Rectangle unionRect = _chunkRecangles[0];
                             _chunkRecangles.Skip(1).ToList().ForEach(rect =>
                                 unionRect = Rectangle.Union(unionRect, rect));

                             if (this.InvokeRequired)
                             {
                                 this.BeginInvoke(new Action<Rectangle>(InvalidateRegion), unionRect);
                             }
                             else
                             {
                                 InvalidateRegion(unionRect);
                             }
                         }
                     }
                 }

             }
             catch (Exception ex)
             {
                 Console.WriteLine($"ChunkScreen error: {ex.Message}");
             }
             finally
             {
                 //clear chunks data
                 _chunkTotalSize = 0;
                 _curChunksSent = 0;
                 _chunkRecangles = new List<Rectangle>();
                 _chunkBitmaps = new List<Bitmap>();
             }
         }
         private void InvalidateRegion(Rectangle rectangle)
         {
             vPictureBox1.Invalidate(rectangle);
         }*/
        private void ChunkScreen(ScreenChunkEntity screenChunk)
        {
            try
            {
                lock (_screenLock)
                {
                    if (_curScreen != null && _screenGraphics != null)
                    {
                        // Check if we have data to process
                        if (screenChunk?.Data == null || screenChunk?.Rects == null ||
                            screenChunk.Data.Count == 0 || screenChunk.Rects.Count == 0)
                        {
                            Console.WriteLine("No screen chunk data to process");
                            return;
                        }

                        // Ensure Data and Rects have same count
                        if (screenChunk.Data.Count != screenChunk.Rects.Count)
                        {
                            Console.WriteLine("Data and Rects count mismatch");
                            return;
                        }

                        // Process each chunk properly - use for loop instead of LINQ abuse
                        for (int i = 0; i < screenChunk.Data.Count; i++)
                        {
                            try
                            {
                                using (MemoryStream stream = new MemoryStream(screenChunk.Data[i]))
                                using (Bitmap bitmap = new Bitmap(stream))
                                {
                                    _screenGraphics.DrawImage(bitmap, screenChunk.Rects[i]);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error processing chunk {i}: {ex.Message}");
                                // Continue processing other chunks
                            }
                        }

                        // Calculate union rectangle safely
                        Rectangle unionRect = screenChunk.Rects[0];
                        for (int i = 1; i < screenChunk.Rects.Count; i++)
                        {
                            unionRect = Rectangle.Union(unionRect, screenChunk.Rects[i]);
                        }

                        // Invalidate the region
                        if (this.InvokeRequired)
                        {
                            this.BeginInvoke(new Action<Rectangle>(InvalidateRegion), unionRect);
                        }
                        else
                        {
                            InvalidateRegion(unionRect);
                        }

                        Console.WriteLine("Finished processing screen chunks");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ChunkScreen error: {ex.Message}");
            }
        }
        //private void ChunkScreen(ScreenChunkEntity screenChunk)
        //{
        //    try
        //    {

        //        lock (_screenLock)
        //        {
        //            if (_curScreen != null && _screenGraphics != null)
        //            {
        //                screenChunk.Data.Zip(screenChunk.Rects, (bytes, rectangle) =>
        //                new
        //                {
        //                    bytes,
        //                    rectangle
        //                }).Where(pair =>
        //                {
        //                    using (MemoryStream stream=  new MemoryStream(pair.bytes))
        //                    using (Bitmap bitmap = new Bitmap(stream))
        //                    {
        //                        _screenGraphics.DrawImage(bitmap, pair.rectangle);
        //                    }
        //                    return true;
        //                }).ToList();

        //                Rectangle unionRect = screenChunk.Rects[0];
        //                screenChunk.Rects.Skip(1).ToList().ForEach(rect =>
        //                    unionRect = Rectangle.Union(unionRect, rect));

        //                if (this.InvokeRequired)
        //                {
        //                    this.BeginInvoke(new Action<Rectangle>(InvalidateRegion), unionRect);
        //                }
        //                else
        //                {
        //                    InvalidateRegion(unionRect);
        //                }
        //                Console.WriteLine("Finished");
        //            }
        //        }

        //        //// Parse data trên background thread
        //        //int totalSize = BitConverter.ToInt32(data, 0);
        //        //int x = BitConverter.ToInt32(data, 4);
        //        //int y = BitConverter.ToInt32(data, 8);
        //        //int width = BitConverter.ToInt32(data, 12);
        //        //int height = BitConverter.ToInt32(data, 16);
        //        //byte[] chunk = new byte[data.Length - 20];
        //        //Buffer.BlockCopy(data, 20, chunk, 0, chunk.Length);

        //        //// Decompression trên background thread
        //        //byte[] dataDecompress = Utils.Decompress(chunk);
        //        //Rectangle rectangle = new Rectangle(x, y, width, height);

        //        //// Drawing trên background thread với lock
        //        //using (MemoryStream ms = new MemoryStream(dataDecompress))
        //        //using (Bitmap jpegBitmap = new Bitmap(ms))
        //        //{
        //        //    lock (_screenLock)
        //        //    {
        //        //        if (_curScreen != null && _screenGraphics != null)
        //        //        {
        //        //            _screenGraphics.DrawImage(jpegBitmap, rectangle);
        //        //        }
        //        //        if (this.InvokeRequired)
        //        //        {
        //        //            this.BeginInvoke(new Action<Rectangle>(InvalidateRegion), rectangle);
        //        //        }
        //        //        else
        //        //        {
        //        //            InvalidateRegion(rectangle);
        //        //        }
        //        //    }
        //        //}

        //        //vPictureBox1.Invalidate(rectangle);
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"ChunkScreen error: {ex.Message}");
        //    }
        //}

        private void InvalidateRegion(Rectangle rectangle)
        {
            vPictureBox1.Invalidate(rectangle);
        }

        public void ScreenEvent(byte[] data)
        {
            // do chạy đồng bộ trên một luồng với socket nên bị bottleneck, cần chạy trên new thread
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<byte[]>(ScreenEvent), data);
                return;
            }

            // UI thread code
            try
            {
                byte[] dataDecompress = Utils.Decompress(data);
                lock (_screenLock)
                {
                    using (MemoryStream stream = new MemoryStream(dataDecompress))
                    {
                        Bitmap image = (Bitmap)Image.FromStream(stream);

                        // Dispose old image to prevent memory leak
                        var oldImage = vPictureBox1.Image;
                        _screenGraphics?.Dispose();
                        _curScreen?.Dispose();

                        _curScreen = new Bitmap(image);
                        _screenGraphics = Graphics.FromImage(_curScreen);

                        InitializeGraphicsSettings();

                        vPictureBox1.Image = _curScreen;


                        oldImage?.Dispose();
                        image?.Dispose();
                    }
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
