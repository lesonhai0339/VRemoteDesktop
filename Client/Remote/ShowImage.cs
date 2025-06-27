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
    public partial class ShowImage : Form
    {
        public ShowImage(Bitmap data)
        {
            InitializeComponent();
            UpdateImage(this,data);
        }
        public void UpdateImage(object sender, Bitmap bitmap)
        {
            pictureBox1.Image?.Dispose(); // Dispose old image
            pictureBox1.Image = (Bitmap)bitmap.Clone(); // Clone to avoid cross-thread/image reuse issues
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
        public EventHandler<Bitmap> UpdateHandler => UpdateImage;
    }
}
