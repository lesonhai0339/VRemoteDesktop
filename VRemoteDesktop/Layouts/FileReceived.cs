using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace VRemoteDesktop.Layouts
{
    public class FileReceivedInfo
    {
        public string FileExtension { get; set; }
        public string Filename { get; set; }
        public long FileSize { get; set; }
    }
    public class FileReceived: TableLayoutPanel
    {
        private PictureBox _fileImageExtension;
        private Label _fileName;
        private Label _fileSize;
        private Button _save;
        private Button _cancel;

        public event EventHandler<EventArgs> ClickedEvent;
        public FileReceived()
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

            this.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            this.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            this.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));

            this.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }
        public void Add(FileReceivedInfo fileInfo)
        {
            if (string.IsNullOrEmpty(fileInfo.FileExtension)
               || string.IsNullOrEmpty(fileInfo.Filename))
            {
                throw new ArgumentException("Missing some arguments");
            }
            _fileImageExtension = new PictureBox();
            using (Icon icon = Helpers.FileHelper.GetFileIconFromFileExtension(fileInfo.FileExtension))
            {
                if (icon != null)
                    _fileImageExtension.Image = icon?.ToBitmap();
                else
                    throw new InvalidOperationException("Cannot get file icon form file extension");
                }
            _fileName = new Label
            {
                Text = Helpers.StringHelper.GenerateStringShortcut(fileInfo.Filename, 30),
                Name = "lbFileName",
                AutoSize = true,
            };
            _fileSize = new Label
            {
                Text = Helpers.StringHelper.GetFileSizeString(fileInfo.FileSize),
                Name = "lbFileSize",
                AutoSize = true,
            };
            _save = new Button
            {
                Text = "Save",
                Name = "btnSave"
            };
            _cancel = new Button
            {
                Text = "Cancel",
                Name = "btnCancel"
            };
            _save.Click += (s, e) =>
            {
                ClickedEvent?.Invoke(s, e);
            };
            _cancel.Click += (s, e) =>
            {
                ClickedEvent?.Invoke(s, e);
            };
            this.Controls.Add(_fileImageExtension, 0, 0);
            this.SetRowSpan(_fileImageExtension, 2);

            this.Controls.Add(_fileName, 1, 0);
            this.Controls.Add(_fileSize, 1, 1);

            this.Controls.Add(_save, 2, 0);
            this.Controls.Add(_cancel, 2, 1);
        }
    }
}
