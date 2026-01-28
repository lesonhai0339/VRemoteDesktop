using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Models;
using static VRemoteDesktop.Utils.DefaultForm;
namespace VRemoteDesktop.Layouts
{
    public class FileAttachmentPanel: TableLayoutPanel
    {
        private readonly Font _defaultFont = new Font("Segoe UI", 9F, FontStyle.Bold);// | FontStyle.Italic);
        private string _socketId;
        private string _id;
        private PictureBox _fileImage;
        private Label _fileName;
        private Label _fileSize;
        private Button _save;
        private Button _cancel;
        private Button _stop;
        private Label _waitingPartnerAccept;
        private VFileInfo _fileInfo;
        private ChatProgressBar _progressBar;

        public event EventHandler<FileReceivedEventArgs> AcceptSaveFile;
        public FileAttachmentPanel(string id, string socketId)
        {
            _id = id;
            _socketId = socketId;
            InitializeComponent();
        }
        public string Id => _id;
        public string SocketId => _socketId;
        public VFileInfo FileInfo => _fileInfo;
        private void InitializeComponent()
        {
            this.Margin = new Padding(0);
            this.Padding = new Padding(0);  
            this.ColumnCount = 3;
            this.RowCount = 2;
            this.Dock = DockStyle.Top;
            this.AutoSize = true;   
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.BorderStyle = BorderStyle.FixedSingle;

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
                Text = fileInfo.Filename,
                Name = "lbFileName",
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                MaximumSize = new Size(200, 40),
            };
            _fileSize = new Label
            {
                Text = Helpers.StringHelper.GetFileSizeString(fileInfo.FileSize),
                Name = "lbFileSize",
                AutoSize = false,
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
                _save.Click += ClickedEventHandler;
                _cancel.Click += ClickedEventHandler;

                this.Controls.Add(_save, 2, 0);
                this.Controls.Add(_cancel, 2, 1);
            }
            else
            {
                _waitingPartnerAccept = new Label
                {
                    AutoSize = false,
                    Text = FORM_WAITING_PARTNER_ACCEPTED,
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
                _stop = new Button
                {
                    Text = "Stop",
                    Name = "btnStop",
                };
                _stop.Click += ClickedEventHandler;
                DisableControl(_cancel);
                this.Controls.Remove(this._save);
                this.Controls.Remove(this._fileSize);

                _progressBar = new ChatProgressBar(_fileInfo);
                _progressBar.ProgressBarEvent += ProgressCompletedEventHandler;
                this.Controls.Add(_stop, 2, 0);
                this.Controls.Add(_progressBar, 1, 1);
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
                    Text = FORM_REJECT_FILE,
                    Name = "lbRejectFile",
                    AutoSize = false,
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
            if (_progressBar != null)
                _progressBar.SetStep(num);
        }
        public void DisableControl(Control control)
        {
            if(control is Button btn)
            {
                btn.Parent.Focus(); 

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
            if (_progressBar != null)
            {
                _progressBar.ProgressBarEvent -= ProgressCompletedEventHandler;
                this.Controls.Remove(_progressBar);
                _progressBar.Dispose();
                _progressBar = null;
            }
            ProgressBarCompletedTask(e.Type);
        }
        public void RemoveProgressBar()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(RemoveProgressBar));
                return;
            }
            if (_progressBar != null)
            {
                _progressBar.ProgressBarEvent -= ProgressCompletedEventHandler;
                this.Controls.Remove(_progressBar);
                _progressBar.Dispose();
                _progressBar = null;
            }
            ProgressBarCompletedTask(ProgressBarEnum.Stop);
        }
        private void ProgressBarCompletedTask(ProgressBarEnum type)
        {
            var oldControl = this.GetControlFromPosition(1, 1);
            if(oldControl != null)
            {
                this.Controls.Remove(oldControl);
                oldControl.Dispose();
            }
            Label btn = new Label
            {
                Text = type == ProgressBarEnum.Finished ? FORM_COMPLETED
                : type == ProgressBarEnum.Error ? FORM_ERROR
                : type == ProgressBarEnum.Timeout ? FORM_TIMEOUT_TITLE
                : type == ProgressBarEnum.Stop ? FORM_STOP                                                                                                                                                              
                : FORM_ERROR,

                AutoSize = true,
                Font = _defaultFont,
                Dock = DockStyle.Fill,
            };
            this.Controls.Add(btn, 1, 1);
            this.DisableControl(_stop);
        }
        public void UpdateRequestSendFileStatus(string text)
        {
            _waitingPartnerAccept.Text = text;
        }
        private void ClickedEventHandler(object sender, EventArgs e)
        {
            string savePath = string.Empty;
            ChatFileType type = ChatFileType.None;
            if (sender is Button btnStop && btnStop == _stop)
            {
                type = ChatFileType.Stop;
            }
            if (sender is Button btnSave && btnSave == _save)
            {
                savePath = Helpers.FileHelper.OpenFileDialogAndSaveFile(_fileInfo.Filename);
                type = ChatFileType.Accept;
            }
            if (sender is Button btnCancel && btnCancel == _cancel)
            {
                type = ChatFileType.Reject;
            }

            AcceptSaveFile?.Invoke(sender, new FileReceivedEventArgs(type, savePath));
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if(_progressBar != null)
                    _progressBar.ProgressBarEvent -= ProgressCompletedEventHandler;

                if(_save != null)
                    _save.Click -= ClickedEventHandler;

                if (_cancel != null)
                    _cancel.Click -= ClickedEventHandler;

                if (_stop != null)
                    _stop.Click -= ClickedEventHandler;

                _fileImage?.Image?.Dispose();
                _defaultFont?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
