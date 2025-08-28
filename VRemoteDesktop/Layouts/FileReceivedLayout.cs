using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;

namespace VRemoteDesktop.Layouts
{
    public class P2PFileReceivedEventArgs: EventArgs
    {
        public P2PFileReceivedEventArgs(bool acceptSave, string filePath)
        {
            this.AcceptSave = acceptSave;
            this.filePath = filePath;
        }
        public bool AcceptSave { get; set; }
        public string filePath { get; set; }
    }
    public class VFileInfo
    {
        public string FileExtension { get; set; }
        public string Filename { get; set; }
        public long FileSize { get; set; }
    }
    public class FileReceivedLayout: TableLayoutPanel
    {
        private PictureBox _fileImageExtension;
        private Label _fileName;
        private Label _fileSize;
        private Button _save;
        private Button _cancel;
        private Label _waitingPartnerAccept;
        private VFileInfo _fileInfo;
        public VProgressBar _progressbar;

        public event EventHandler<P2PFileReceivedEventArgs> ClickedEvent;
        public FileReceivedLayout()
        {
            InitializeComponent();
        }
        private void InitializeComponent()
        {
            this.BackColor = Color.Yellow;
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
        public void Add(VFileInfo fileInfo, bool isSender = false)
        {
            if (string.IsNullOrEmpty(fileInfo.FileExtension)
               || string.IsNullOrEmpty(fileInfo.Filename))
            {
                throw new ArgumentException("Missing some arguments");
            }
            _fileInfo = fileInfo;
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

            this.Controls.Add(_fileImageExtension, 0, 0);
            this.SetRowSpan(_fileImageExtension, 2);

            this.Controls.Add(_fileName, 1, 0);
            this.Controls.Add(_fileSize, 1, 1);

            if (!isSender)
            {
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
                    string savePath = Helpers.FileHelper.OpenFileDialogAndSaveFile(_fileInfo.Filename);
                    ClickedEvent?.Invoke(s, new P2PFileReceivedEventArgs(true, savePath));
                };
                _cancel.Click += (s, e) =>
                {
                    ClickedEvent?.Invoke(s, new P2PFileReceivedEventArgs(false, null));
                };

                this.Controls.Add(_save, 2, 0);
                this.Controls.Add(_cancel, 2, 1);
            }
            else
            {
                _waitingPartnerAccept = new Label
                {
                    AutoSize = true,
                    Text = "Chờ đối tác xác nhận..."
                };

                this.Controls.Add(_waitingPartnerAccept, 2, 0);
                this.SetRowSpan(_waitingPartnerAccept, 2);
            }
        }
        public void RemoveButton()
        {
            this.Controls.Remove(this._save);
            this.Controls.Remove(this._cancel);
            this.Controls.Remove(this._fileSize);
            _progressbar = new VProgressBar(_fileInfo);
            _progressbar.ProgressCompleted += ProgressCompletedEventHandler;
            this.Controls.Add(_progressbar, 1, 1);
            this.SetColumnSpan(_progressbar, 2);
        }
        public void UpdateProgressBar(int num)
        {
            if (_progressbar != null)
                _progressbar.SetStep(num);
        }
        private void ProgressCompletedEventHandler(object sender, EventArgs e)
        {
           if(_progressbar != null)
            {
                _progressbar.ProgressCompleted -= ProgressCompletedEventHandler;
                this.Controls.Remove(_progressbar);
                _progressbar.Dispose();
                _progressbar = null;
            }
            FileTaskCompleted();
        }
        private void FileTaskCompleted()
        {
            Label btn = new Label
            {
                Text = "Completed",
                AutoSize = true
            };
            this.Controls.Add(btn, 2, 0);
            this.SetRowSpan(btn, 2);
        }
        public void UpdateRequestSendFileStatus(string text)
        {
            _waitingPartnerAccept.Text = text;
        }
    }
}
