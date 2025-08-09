using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace VRemoteClient.Models.CustomLayouts
{
    public class CustomFileTemplate: TableLayoutPanel
    {
        private ProgressBar _progressBar;
        private PictureBox _picturebox;
        private Label _fileName;
        private Label _fileSize;
        private Button _open;
        private Button _save;
        private Button _cancel;
        private string _sessionId;
        private string _tempFilePath;
        private byte[] _tempFileData;

        private Action<bool, string> _acceptOrRejectFileReceive;
        public CustomFileTemplate(Action<bool, string> callback)
        {
            InitializeComponent();
            _acceptOrRejectFileReceive = callback;  
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

            this.ColumnStyles.Add(new RowStyle(SizeType.AutoSize));
            this.ColumnStyles.Add(new RowStyle(SizeType.AutoSize));


            _picturebox = new PictureBox();
            _fileName = new Label();
            _fileSize = new Label();
            _open = new Button();
            _save = new Button();
            _cancel = new Button();

            _fileName.AutoSize = true;
            _open.BackColor = Color.White;
            _open.Text = "Open";
            _open.Name = "btnOpen";
            _save.BackColor = Color.White;
            _save.Text = "Save";
            _open.Name = "btnSave";
            _cancel.BackColor = Color.White;
            _cancel.Text = "Cancel";
            _open.Name = "btnCancel";

            _open.Click += ButtonEvent;
            _save.Click += ButtonEvent;
            _cancel.Click += ButtonEvent;
        }
        public Control ReceivedFileSentFromPartner(string sessionId, byte[] data)
        {
            string dataString = Encoding.ASCII.GetString(data);
            string[] array = Utils.StringBuilderUtils.StringToStringArrayWithSeparator(dataString);
            _sessionId = sessionId;

            string tempFilePath = Path.Combine(Path.GetTempPath(), array[0]);
            if(!File.Exists(tempFilePath))
                File.WriteAllBytes(tempFilePath, new byte[0]);

            Icon icon = Icon.ExtractAssociatedIcon(tempFilePath);

            _picturebox.Image = icon.ToBitmap();
            _fileName.Text = Utils.StringBuilderUtils.GenerateStringShortcut(array[0]);
            _fileSize.Text = Utils.StringBuilderUtils.GetFileSizeString(long.Parse(array[1]));

            this.Controls.Add(_picturebox, 0, 0);
            this.SetRowSpan(_picturebox, 2);


            this.Controls.Add(_fileName, 1, 0);
            this.Controls.Add(_fileSize, 1, 1);

            this.Controls.Add(_open, 2, 0);
            this.Controls.Add(_save, 2, 1);

            return this;
        }
        private void ButtonEvent(object sender , EventArgs e)
        {
            if(sender is Button btn)
            {
                if (string.IsNullOrEmpty(_sessionId))
                    return;

                if(btn.Name == "btnSave")
                {
                    string filePath = Utils.FileUtils.OpenFileDialogAndSaveFile();
                    if(!string.IsNullOrEmpty(filePath))
                    {
                        _tempFilePath = filePath;
                        _acceptOrRejectFileReceive?.Invoke(true, _sessionId);
                        RemoveControl(btn, _cancel);
                    }
                    else
                    {
                        MessageBox.Show("Please select a valid file path to save the received file.", "Invalid Path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else if(btn.Name == "btnCancel")
                {
                    _acceptOrRejectFileReceive?.Invoke(false, _sessionId);
                    RemoveControl(btn, _save);
                }
                else if(btn.Name == "btnOpen")
                {

                }
            }
        }
        private void RemoveControl(params Control[] controls)
        {
           foreach(Control control in controls)
            {
                this.Controls.Remove(control);
                control.Dispose();
            }
        }
        public Control FilePrepareSendToPartner(string path)
        {
            FileInfo fileInfo = new FileInfo(path);
            Icon icon = Icon.ExtractAssociatedIcon(path);

            PictureBox picturebox = new PictureBox();
            picturebox.Image = icon.ToBitmap();

            Label fileName = new Label
            {
                Text = Utils.StringBuilderUtils.GenerateStringShortcut(fileInfo.Name),
                AutoSize = true
            };
            Label fileSize = new Label();
            fileSize.Text = Utils.StringBuilderUtils.GetFileSizeString(fileInfo.Length);

            this.Controls.Add(picturebox, 0, 0);
            this.SetRowSpan(picturebox, 2);


            this.Controls.Add(fileName, 1, 0);
            this.Controls.Add(fileSize, 1, 1);

            return this;
        }
    }
}
