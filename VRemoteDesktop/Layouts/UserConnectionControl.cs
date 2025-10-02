using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace VRemoteDesktop.Layouts
{
    public class UserConnectionControl: TableLayoutPanel
    {
        private string _connectionId;
        private Label _connectionName;
        private int _unreadNum  = 0;
        private Panel _unreadMessage;
        private Label _numberUnreadMessages;
        public UserConnectionControl(string connectionId, string name, int width, int height)
        {
            _connectionId = connectionId;
            this.Name = _connectionId;
            this.Width = width;
            this.Height = height;
            Init(name);
        }
        private void Init(string name)
        {
            this.Padding = new Padding(0);
            this.Margin = new Padding(0);
            this.ColumnCount = 2;
            this.RowCount = 1;

            this.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 90F));
            this.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));

            this.RowStyles.Add(new RowStyle(SizeType.AutoSize));


            _connectionName = new Label
            {
                Text = name,
            };
            _unreadMessage = new Panel
            {
                Size = new Size(this.Height -1, this.Height -1),
                BackColor = Color.Transparent,
                Visible = false,
                Anchor = AnchorStyles.None, 
                Margin = new Padding(0)  
            };
            _numberUnreadMessages = new Label
            {
                Text = (_unreadNum == 0) ? "" : _unreadNum.ToString(),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            _unreadMessage.Controls.Add(_numberUnreadMessages);
            _unreadMessage.Paint += UpdatePanel;

            this.Controls.Add(_connectionName, 0, 0);
            this.Controls.Add(_unreadMessage, 1, 0);
            RegisterEvent(this);
        }

        private void UpdatePanel(object sender, PaintEventArgs e)
        {
            if (_unreadNum <= 0)
                return;

            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Color circleColor = (_unreadNum > 0) ? Color.Red : Color.LightSkyBlue;
            using (SolidBrush brush = new SolidBrush(circleColor))
            {
                g.FillEllipse(brush, 1, 1, _unreadMessage.Width - 2, _unreadMessage.Height - 2);
            }
        }
        public void UpdateUnreadCount(int num)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<int>(UpdateUnreadCount), num);
                return;
            }
            _unreadNum += num;

            _numberUnreadMessages.Text = (_unreadNum > 0) ? _unreadNum.ToString() : "";
            _unreadMessage.Invalidate();
            _unreadMessage.Visible = (_unreadNum > 0) ? true : false;
        }
        public void ClearCount()
        {
            _unreadNum = 0;
        }
        private void RegisterEvent(Control control)
        {
            control.Click += (s, e) => base.OnClick(e);

            foreach (Control child in control.Controls)
            {
                RegisterEvent(child);
            }
        }
        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // UserConnectionControl
            // 
            this.Name = "UserConnectionControl";
            this.Size = new System.Drawing.Size(248, 35);
            this.ResumeLayout(false);

        }
    }
}
