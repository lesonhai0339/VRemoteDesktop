using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace VRemoteClient.Models.CustomLayouts
{
    public class FileTemplate : TableLayoutPanel
    {
        public FileTemplate()
        {
            InitializeComponent();
        }
        private void InitializeComponent()
        {
            this.ColumnCount = 3;
            this.RowCount = 2;
            this.Dock = DockStyle.Fill;

            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            for (int i = 0; i < 3; i++)
            {
                if (i == 0)
                {
                    this.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
                }
                else if(i == 1)
                {
                    this.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                }
                else
                {
                    this.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
                }
            }
            for (int i = 0; i < 2; i++)
            {
                this.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
        }
        public Control GenerateFileContent(string path)
        {
            FileInfo fileInfo = new FileInfo(path);
            Icon icon = Icon.ExtractAssociatedIcon(path);

            PictureBox picturebox = new PictureBox();
            picturebox.Image = icon.ToBitmap();

            Label fileName = new Label
            {
                Text = fileInfo.Name,
                AutoSize = true           
            };
            Label fileSize = new Label();
            fileSize.Text = Utils.StringBuilderUtils.GetFileSizeString(fileInfo.Length);

            Button open = new Button
            {
                Name = "btnSave",
                Text = "Save",
                BackColor = Color.White,
            };

            Button save = new Button
            {
                Name = "btnCancel",
                Text = "Cancel",
                BackColor = Color.White
            };
            this.Controls.Add(picturebox, 0,0);
            this.SetRowSpan(picturebox, 2);


            this.Controls.Add(fileName, 1, 0);
            this.Controls.Add(fileSize, 1, 1);

            this.Controls.Add(open, 2, 0);
            this.Controls.Add(save, 2, 1);

            return this;
        }
    }
}
