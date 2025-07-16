using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace VRemoteClient.Models.Entities
{
    public class VPicturebox: PictureBox
    {
        public void EnableOptimizedDoubleBuffering()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            this.UpdateStyles();
        }
    }
}
