using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteClient.Models.CustomLayouts;
using VRemoteClient.Models.Entities;
using VRemoteClient.Services.RemoteDesktopService;

namespace VRemoteClient
{
    public partial class FormChat : Form
    {
        private RemoteDesktop _remoteDesktop;
        private ConnectionInfo _connectionInfo;
        public FormChat(RemoteDesktop remoteDesktop, ConnectionInfo connectionInfo)
        {
            InitializeComponent();
            Init(remoteDesktop, connectionInfo);
            this.Text = "Chat Window";
            this.StartPosition = FormStartPosition.Manual;
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            this.BackColor = Color.White;
        }
        private void Init(RemoteDesktop remoteDesktop, ConnectionInfo connectionInfo)
        {
            _remoteDesktop ??= remoteDesktop;
            _connectionInfo ??= connectionInfo;
        }
        private void ConfigDefaultFormPosition()
        {
            Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;

            this.Location = new Point(
                workingArea.Right - this.Width - 20,
                workingArea.Bottom - this.Height - 120
            );

            fpnChat.FlowDirection = FlowDirection.TopDown;
            fpnChat.WrapContents = false;
            fpnChat.AutoScroll = true;
            fpnChat.BorderStyle = BorderStyle.FixedSingle;
        }
        private void FormChat_Load(object sender, EventArgs e)
        {
            ConfigDefaultFormPosition();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string userName = "Anonymous";
            string data = this.txtChatContent.Text;
            Label lb = new Label();
            lb.MaximumSize = new Size(); ;
            CustomRichTextBox tb = new CustomRichTextBox()
                .SetMargin(5)
                .Addcontent(userName, true)
                .Addcontent(DateTime.Now.ToString("( hh:mm:ss - dd/MM/yyyy ): "))
                .Addcontent(data)
                .SetAutoHeight(fpnChat.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 10);
            fpnChat.Controls.Add(tb);

            _remoteDesktop.AddWork(new TaskObject
            {
                TaskType = Models.Enums.SocketDataType.Message,
                Data = Encoding.UTF8.GetBytes(tb.Text),
                SessionId = _connectionInfo.SessionId,
                IsSendHeader = true
            }, Models.Enums.DataType.Command);
        }
    }
}
