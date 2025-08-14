using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace VRemoteClient.Models.CustomLayouts
{
    public class CustomPanel: Panel
    {
        public CustomPanel()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Width = 15;
            this.Height = 15;
        }

        public void CreateCircle(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Color color = Color.Green;

            using (SolidBrush brush = new SolidBrush(color))
            {
                e.Graphics.FillEllipse(brush,
                    0,
                    0,
                    this.Width,
                    this.Height
                );
            }
        }
    }
}
