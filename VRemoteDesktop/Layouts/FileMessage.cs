using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace VRemoteDesktop.Layouts
{
    public class FileMessage: TableLayoutPanel
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

        private Action<bool, string> _acceptOrRejectFileReceive;
        public FileMessage()
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
            this.RowStyles.Add(new RowStyle(SizeType.AutoSize));

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
            _save.Name = "btnSave";
            _cancel.BackColor = Color.White;
            _cancel.Text = "Cancel";
            _cancel.Name = "btnCancel";

            _open.Click += ButtonEvent;
            _save.Click += ButtonEvent;
            _cancel.Click += ButtonEvent;

            _progressBar = new ProgressBar();
            _progressBar.Visible = false;
            _progressBar.Minimum = 0;
            _progressBar.Maximum = 100;
            _progressBar.Step = 1;
        }
        private void ButtonEvent(object sender, EventArgs e)
        {
            if (sender is Button btn)
            {
                if (btn.Name == "btnSave")
                {
                   
                }
                else if (btn.Name == "btnCancel")
                {
                }
                else if (btn.Name == "btnOpen")
                {

                }
            }
        }
    }
}
