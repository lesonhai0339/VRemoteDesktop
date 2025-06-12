using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Client.FFmpeg.ScreenA
{
    public class CellData
    {
        public int X { get; set; }
        public int Y { get; set; }
        public byte[] Data { get; set; }
    }
    public  class Capture
    {
        private const int CELL_SIZE = 64;

        public Capture() { }

        public unsafe List<CellData> SplitCaptureToCellsUnsafe(Bitmap capture)
        {
            var rect = new Rectangle(0, 0, capture.Width, capture.Height);
            var bmpData = capture.LockBits(rect, ImageLockMode.ReadOnly, capture.PixelFormat);

            int bytesPerPixel = Image.GetPixelFormatSize(capture.PixelFormat) / 8;
            int cellsX = capture.Width / CELL_SIZE;
            int cellsY = capture.Height / CELL_SIZE;

            List<CellData> cells = new List<CellData>();

            byte* basePtr = (byte*)bmpData.Scan0;

            for (int cellY = 0; cellY < cellsY; cellY++)
            {
                for (int cellX = 0; cellX < cellsX; cellX++)
                {
                    byte[] cellData = new byte[CELL_SIZE * CELL_SIZE * bytesPerPixel];

                    fixed (byte* cellPtr = cellData)
                    {
                        for (int row = 0; row < CELL_SIZE; row++)
                        {
                            byte* sourceRow = basePtr +
                                (cellY * CELL_SIZE + row) * bmpData.Stride +
                                cellX * CELL_SIZE * bytesPerPixel;

                            byte* destRow = cellPtr + row * CELL_SIZE * bytesPerPixel;

                            // Copy cả row một lúc
                            Buffer.MemoryCopy(sourceRow, destRow,
                                CELL_SIZE * bytesPerPixel,
                                CELL_SIZE * bytesPerPixel);
                        }
                    }

                    cells.Add(new CellData { X = cellX, Y = cellY, Data = cellData });
                }
            }

            capture.UnlockBits(bmpData);
            return cells;
        }
        public Bitmap CaptureScreen()
        {
            Rectangle bounds = Screen.PrimaryScreen.Bounds;
            Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            Graphics graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            return bitmap;
        }
        public static byte[] BitmapToByteArray(Bitmap bitmap, out int stride)
        {

            BitmapData bmpdata = null;

            try
            {
                bmpdata = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
                stride = bmpdata.Stride;
                int numbytes = bmpdata.Stride * bitmap.Height;
                byte[] bytedata = new byte[numbytes];
                IntPtr ptr = bmpdata.Scan0;

                Marshal.Copy(ptr, bytedata, 0, numbytes);

                return bytedata;
            }
            finally
            {
                if (bmpdata != null)
                    bitmap.UnlockBits(bmpdata);
            }

        }
    }
}
