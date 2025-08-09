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

        }
        public Control ReceivedFileSentFromPartner(string sessionId, byte[] data)
        {
            string dataString = Encoding.ASCII.GetString(data);
            string[] array = Utils.StringBuilderUtils.StringToStringArrayWithSeparator(dataString);

            string tempFilePath = Path.Combine(Path.GetTempPath(), array[0]);
            if(!File.Exists(tempFilePath))
                File.WriteAllBytes(tempFilePath, new byte[0]);

            Icon icon = Icon.ExtractAssociatedIcon(tempFilePath);

            PictureBox picturebox = new PictureBox();
            picturebox.Image = icon.ToBitmap();

            Label fileName = new Label
            {
                Text = Utils.StringBuilderUtils.GenerateStringShortcut(array[0]),
                AutoSize = true
            };
            Label fileSize = new Label();
            fileSize.Text = Utils.StringBuilderUtils.GetFileSizeString(long.Parse(array[1]));

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
            open.Click += (sender, e) => {
                _acceptOrRejectFileReceive?.Invoke(true, sessionId);
                RemoveControl(save, open);
            };

            save.Click += (sender, e) => {
                _acceptOrRejectFileReceive?.Invoke(false, sessionId);
                RemoveControl(save, open);
            };


            this.Controls.Add(picturebox, 0, 0);
            this.SetRowSpan(picturebox, 2);


            this.Controls.Add(fileName, 1, 0);
            this.Controls.Add(fileSize, 1, 1);

            this.Controls.Add(open, 2, 0);
            this.Controls.Add(save, 2, 1);

            return this;
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
