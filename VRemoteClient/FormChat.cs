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
using VRemoteClient.Models.Enums;
using VRemoteClient.Services.RemoteDesktopService;
using VRemoteClient.Utils;

namespace VRemoteClient
{
    public partial class FormChat : Form
    {
        private readonly object _lockObject = new object();
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
            RemoteDesktop ??= remoteDesktop;
            _connectionInfo ??= connectionInfo;
        }
        #region Properties
        public RemoteDesktop RemoteDesktop
        {
            get
            {
                lock (_lockObject)
                {
                    return _remoteDesktop;
                }
            }
            set
            {
                lock (_lockObject)
                {
                    if (_remoteDesktop != null)
                    {
                        _remoteDesktop.ChatMessageEvent -= ChatMessageEventHandler;
                    }
                    _remoteDesktop = value;
                    if(_remoteDesktop != null)
                    {
                        _remoteDesktop.ChatMessageEvent += ChatMessageEventHandler;
                    }
                }
            }
        }

        private void ChatMessageEventHandler(byte[] obj)
        {
            string message = Encoding.UTF8.GetString(obj);
            AddChatMessage("Sender", message);
        }
        #endregion
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
        }
        private void FormChat_Shown(object sender, EventArgs e)
        {
            ConfigDefaultFormPosition();
        }
        private string AddChatMessage(string userName, string data)
        {
            if (fpnChat.InvokeRequired)
            {
                return (string)fpnChat.Invoke(new Func<string>(() => AddChatMessage(userName, data)));
            }

            CustomRichTextBox tb = new CustomRichTextBox()
                .SetMargin(5)
                .Addcontent(userName, true)
                .Addcontent(DateTime.Now.ToString("( hh:mm:ss - dd/MM/yyyy ): "))
                .Addcontent(data)
                .SetAutoHeight(fpnChat.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 10);
            fpnChat.Controls.Add(tb);
            return tb.Text;
        }
        private void button1_Click(object sender, EventArgs e)
        {
            string userName = "Me";
            string data = this.txtChatContent.Text;

            var tb = AddChatMessage(userName, data);
            AddWork(SocketDataType.Message, Encoding.ASCII.GetBytes(tb));
        }
        private void AddWork(SocketDataType type, byte[] data)
        {
            _remoteDesktop.AddWork(new TaskObject
            {
                TaskType = type,
                Data = data,
                SessionId = _connectionInfo.SessionId,
                IsSendHeader = true
            }, Models.Enums.DataType.Command);
        }

        private void btnSendAttachment_Click(object sender, EventArgs e)
        {
            using (var dialog= new OpenFileDialog())
            {
                DialogResult result = dialog.ShowDialog();
                if(result == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    string selectedPath = dialog.FileName;
                    try
                    {
                        var response = ByteArrayUtils.FileToByteArray(selectedPath).GetResult(); ;
                        byte[] compressed = ByteArrayUtils.Compress(response).GetResult();
                        string hashedString = StringBuilderUtils.SHAHash(compressed);
                        byte[] hashed = Encoding.ASCII.GetBytes(hashedString);
                        var combined = ByteArrayUtils.Combine(hashed, compressed).GetResult();

                        AddWork(SocketDataType.FileTransfer, combined);
                    }
                    catch(InvalidOperationException ex)
                    {
                        MessageBox.Show($"Error: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error: {ex.Message}");
                    }
                }
                else
                {
                    MessageBox.Show("Lỗi không xác định");
                }
            }
        }
    }
}
