using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Client
{
    public class WindowsScreen
    {
        public byte[] GrabDesktop()
        {
            Rectangle bound = Screen.PrimaryScreen.Bounds;
            Bitmap screenshot = new Bitmap(bound.Width, bound.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            Graphics graphics = Graphics.FromImage(screenshot);
            graphics.CopyFromScreen(bound.X, bound.Y, 0, 0, bound.Size, CopyPixelOperation.SourceCopy);


            using (MemoryStream stream = new MemoryStream())
            {
                screenshot.Save(stream, ImageFormat.Jpeg);
                return stream.ToArray();
            }
        }
    }
}
