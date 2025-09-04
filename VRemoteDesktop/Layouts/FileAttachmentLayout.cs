using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.Events;
using VRemoteDesktop.Models;
using static System.Net.Mime.MediaTypeNames;

namespace VRemoteDesktop.Layouts
{
    public class FileAttachmentLayout: TableLayoutPanel
    {
        private readonly Font _defaultFont = new Font("Segoe UI", 9F, FontStyle.Bold);// | FontStyle.Italic);
        private string _id;
        private PictureBox _fileImage;
        private Label _fileName;
        private Label _fileSize;
        private Button _save;
        private Button _cancel;
        private Label _waitingPartnerAccept;
        private VFileInfo _fileInfo;
        private VProgressBar _progressbar;

        public event EventHandler<P2PFileReceivedEventArgs> AcceptSaveFile;
        public FileAttachmentLayout(string id)
        {
            _id = id;
            InitializeComponent();
        }
        public string Id => _id;
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
            _fileImage = new PictureBox();
            using (Icon icon = Helpers.FileHelper.GetFileIconFromFileExtension(fileInfo.FileExtension))
            {
                if (icon != null)
                    _fileImage.Image = icon?.ToBitmap();
                else
                    throw new InvalidOperationException("Cannot get file icon form file extension");
            }
            _fileName = new Label
            {
                Text = Helpers.StringHelper.GenerateStringShortcut(fileInfo.Filename, 30),
                Name = "lbFileName",
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            _fileSize = new Label
            {
                Text = Helpers.StringHelper.GetFileSizeString(fileInfo.FileSize),
                Name = "lbFileSize",
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
            };

            this.Controls.Add(_fileImage, 0, 0);
            this.SetRowSpan(_fileImage, 2);

            this.Controls.Add(_fileName, 1, 0);
            this.Controls.Add(_fileSize, 1, 1);

            if (!isSender)
            {
                _save = new Button
                {
                    Text = "Save",
                    Name = "btnSave",
                };
                _cancel = new Button
                {
                    Text = "Cancel",
                    Name = "btnCancel"
                };
                _save.Click += (s, e) =>
                {
                    string savePath = Helpers.FileHelper.OpenFileDialogAndSaveFile(_fileInfo.Filename);
                    AcceptSaveFile?.Invoke(s, new P2PFileReceivedEventArgs(true, savePath));
                };
                _cancel.Click += (s, e) =>
                {
                    AcceptSaveFile?.Invoke(s, new P2PFileReceivedEventArgs(false, null));
                };

                this.Controls.Add(_save, 2, 0);
                this.Controls.Add(_cancel, 2, 1);
            }
            else
            {
                _waitingPartnerAccept = new Label
                {
                    AutoSize = true,
                    Text = "Chờ đối tác xác nhận...",
                    Font = _defaultFont,
                    Dock = DockStyle.Fill
                };

                this.Controls.Add(_waitingPartnerAccept, 2, 0);
                this.SetRowSpan(_waitingPartnerAccept, 2);
            }
        }
        public void AcceptSendFile()
        {
            try
            {
                DisableControl(_save);
                DisableControl(_cancel);
                this.Controls.Remove(this._fileSize);

                _progressbar = new VProgressBar(_fileInfo);
                _progressbar.ProgressBarEvent += ProgressCompletedEventHandler;
                this.Controls.Add(_progressbar, 1, 1);
            }
            finally
            {
                _fileSize?.Dispose();
                _fileSize = null;
            }
        }
        public void RejectSendFile()
        {
            try
            {
                DisableControl(_save);
                DisableControl(_cancel);
                this.Controls.Remove(_fileSize);
                Label rj = new Label
                {
                    Text = "Đã từ chối file",
                    Name = "lbRejectFile",
                    AutoSize = true,
                    Font = _defaultFont,
                    Dock = DockStyle.Fill,
                };
                this.Controls.Add(rj, 1, 1);
            }
            finally
            {
                _fileSize?.Dispose();
                _fileSize = null;
            }
        }
        public void UpdateProgressBar(int num)
        {
            if (_progressbar != null)
                _progressbar.SetStep(num);
        }
        private void DisableControl(Control control)
        {
            if(control is Button btn)
            {
                btn.Enabled = false;
                btn.FlatStyle = FlatStyle.Flat;
                btn.TabStop = false;
                btn.Text = "";
                btn.FlatAppearance.BorderSize = 0;
                btn.BackColor = this.BackColor;
            }
        }
        private void ProgressCompletedEventHandler(object sender, ChatProgressBarEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<object, ChatProgressBarEventArgs>(ProgressCompletedEventHandler), sender, e);
                return;
            }
            if (_progressbar != null)
            {
                _progressbar.ProgressBarEvent -= ProgressCompletedEventHandler;
                this.Controls.Remove(_progressbar);
                _progressbar.Dispose();
                _progressbar = null;
            }
            ProgressBarCompletedTask(e.Type);
        }
        private void ProgressBarCompletedTask(ProgressbarEnum type)
        {
            var oldControl = this.GetControlFromPosition(1, 1);
            if(oldControl != null)
            {
                this.Controls.Remove(oldControl);
                oldControl.Dispose();
            }
            Label btn = new Label
            {
                Text = (type == ProgressbarEnum.Finished) ? "Hoàn thành" : "Xảy ra lỗi",
                AutoSize = true,
                Font = _defaultFont,
                Dock = DockStyle.Fill,
            };
            this.Controls.Add(btn, 1, 1);
        }
        public void UpdateRequestSendFileStatus(string text)
        {
            _waitingPartnerAccept.Text = text;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if(_progressbar != null)
                    _progressbar.ProgressBarEvent -= ProgressCompletedEventHandler;
            }
            base.Dispose(disposing);
        }
    }
}
