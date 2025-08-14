using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace VRemoteClient.Models.CustomLayouts
{
    public class TextTemplate: TableLayoutPanel
    {
        private Label _userName;
        private Label _message;
        public TextTemplate(string userName, string message)
        {
            if(string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(message))
            {
                throw new ArgumentException("User name and message cannot be null or empty.");  
            }

            InitializeComponent();
            SetUserName(userName);
            SetMessage(message);
        }
        private void InitializeComponent()
        {
            this.ColumnCount = 1;
            this.RowCount = 2;
            this.Dock = DockStyle.Fill;

            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            this.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _userName = new Label();
            _userName.AutoSize = true;
            _userName.BackColor = Color.White;
            _userName.BorderStyle = BorderStyle.None;
            _userName.Font = new Font("Arial", 10, FontStyle.Bold);

            _message = new Label();
            _message.AutoSize = true;
            _message.BackColor = Color.White;
            _message.BorderStyle = BorderStyle.None;
            _message.Font = new Font("Arial", 10, FontStyle.Regular);
        }
        public void SetUserName(string userName)
        {
            _userName.Text = userName;
            this.Controls.Add(_userName, 0, 0);
        }
        public void SetMessage(string message)
        {
            _message.Text = message;
            this.Controls.Add(_message, 0, 1);
        }
    }
}
