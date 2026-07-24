using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Vsign4.VRemoteDesktop.Enums;
using Vsign4.VRemoteDesktop.Events;
using Vsign4.VRemoteDesktop.Presenters.Enums;
using Vsign4.VRemoteDesktop.Services.FileService.DTOs;

namespace Vsign4.VRemoteDesktop.Layouts
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
        private EventHandler _parentResizeHandler;

        public event EventHandler<FileReceivedEventArgs> AcceptSaveFile;
        public FileAttachmentPanel(string id, string socketId)
        {
            _id = id;
            _socketId = socketId;
            InitializeComponent();
        }
        public string Id
        {
            get
            {
                return _id; 
            }
        }
        public string SocketId
        {
            get
            {
                return _socketId;
            }
        }
        public VFileInfo FileInfo
        {
            get
            {
                return _fileInfo;       
            }
        }
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

            //Icon and action columns take only the room they need; the name/progress
            //column absorbs the rest. This prevents a fixed-width button in a percent
            //column from forcing the whole panel wider than its parent.
            this.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            this.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            this.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }
        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);

            if (_parentResizeHandler == null)
                _parentResizeHandler = (s, ev) => FitToParentWidth();

            if (this.Parent != null)
            {
                this.Parent.SizeChanged -= _parentResizeHandler;
                this.Parent.SizeChanged += _parentResizeHandler;
                FitToParentWidth();
            }
        }
        private void FitToParentWidth()
        {
            if (this.Parent == null) return;

            int width = this.Parent.ClientSize.Width - this.Margin.Horizontal;
            if (width <= 0) return;

            //Pin the width to the parent (min == max) so the AutoSize table only grows
            //vertically. The name column ellipsizes instead of overflowing horizontally.
            this.MinimumSize = new Size(width, 0);
            this.MaximumSize = new Size(width, 0);
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
                    _fileImage.Image = icon.ToBitmap();
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
                    AutoSize = true,
                    Text = "Vui lòng chờ đối tác xác nhận",
                    Font = _defaultFont,
                    TextAlign = ContentAlignment.MiddleCenter,
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
                if(_fileSize != null)
                    _fileSize.Dispose();
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
                    Text = "Đối tác đã từ chối",
                    Name = "lbRejectFile",
                    AutoSize = false,
                    Font = _defaultFont,
                    Dock = DockStyle.Fill,
                };
                this.Controls.Add(rj, 1, 1);
            }
            finally
            {
                _fileSize = null; 
                if (_fileSize != null)
                    _fileSize.Dispose();
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
            Button btn = control as Button;
            if(btn != null)
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
                Text = type == ProgressBarEnum.Finished ? "Hoàn thành"
                : type == ProgressBarEnum.Error ? "Xảy ra lỗi"
                : type == ProgressBarEnum.Timeout ? "Quá thời gian"
                : type == ProgressBarEnum.Stop ? "Đã dừng"                                                                                                                                                              
                : "Xảy ra lỗi",

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

            Button btn = sender as Button;  
            if (btn != null && btn == _stop)
            {
                type = ChatFileType.Stop;
            }
            if (btn != null && btn == _save)
            {
                savePath = Helpers.FileHelper.OpenFileDialogAndSaveFile(_fileInfo.Filename);
                type = ChatFileType.Accept;
            }
            if (btn != null && btn == _cancel)
            {
                type = ChatFileType.Reject;
            }

            if(AcceptSaveFile != null)
                AcceptSaveFile.Invoke(sender, new FileReceivedEventArgs(type, savePath));
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.Parent != null && _parentResizeHandler != null)
                    this.Parent.SizeChanged -= _parentResizeHandler;

                if(_progressBar != null)
                    _progressBar.ProgressBarEvent -= ProgressCompletedEventHandler;

                if(_save != null)
                    _save.Click -= ClickedEventHandler;

                if (_cancel != null)
                    _cancel.Click -= ClickedEventHandler;

                if (_stop != null)
                    _stop.Click -= ClickedEventHandler;

                if(_fileImage != null && _fileImage.Image != null)
                {
                    _fileImage.Image.Dispose();
                }
                if(_defaultFont != null)
                    _defaultFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
