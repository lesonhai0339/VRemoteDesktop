using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteClient.Models.CustomLayouts;
using VRemoteClient.Models.Entities;
using VRemoteClient.Models.Enums;
using VRemoteClient.Services.RemoteDesktopService;
using VRemoteClient.Utils;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

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
            //AddChatMessage("Sender", message);
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
            fpnChat.Padding = new Padding(0, 0, SystemInformation.VerticalScrollBarWidth, 0);

        }
        private void FormChat_Load(object sender, EventArgs e)
        {
        }
        private void FormChat_Shown(object sender, EventArgs e)
        {
            ConfigDefaultFormPosition();
        }
        private void button1_Click(object sender, EventArgs e)
        {
            //CustomMessage cs = new CustomMessage(ChatMessageType.TEXT, fpnChat.Size, this.txtChatContent.Text);
           
            TextTemplate cs = new TextTemplate("Tôi: ", this.txtChatContent.Text);
            fpnChat.Controls.Add(cs);


            //string userName = "Me";
            //string data = this.txtChatContent.Text;

            //var tb = AddChatMessage(userName, data);
            //AddWork(SocketDataType.Message, Encoding.ASCII.GetBytes(tb));
        }
        private void AddElementToLayout(Control control)
        {
            if (control != null)
            {
                fpnChat.Controls.Add(control);
            }
        }
        private void AddWork(SocketDataType type, byte[] data)
        {
            return;
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
            using (var dialog = new OpenFileDialog())
            {
                DialogResult result = dialog.ShowDialog();
                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    string selectedPath = dialog.FileName;
                    try
                    {
                        string data = GetFileInfo(selectedPath);


                        //var response = ByteArrayUtils.FileToByteArray(selectedPath).GetResult(); ;
                        //byte[] compressed = ByteArrayUtils.Compress(response).GetResult();
                        //string hashedString = StringBuilderUtils.SHAHash(compressed);
                        //byte[] hashed = Encoding.ASCII.GetBytes(hashedString);
                        //var combined = ByteArrayUtils.Combine(hashed, compressed).GetResult();

                        //CustomMessage cs = new CustomMessage();
                        //fpnChat.Controls.Add(cs);

                        //var tb = AddChatMessage(userName, data);


                        //AddWork(SocketDataType.RequestSendFile, Encoding.ASCII.GetBytes(data));

                        CustomFileTemplate cs = new CustomFileTemplate();
                        var table = cs.FilePrepareSendToPartner(selectedPath);
                        fpnChat.Controls.Add(table);
                    }
                    catch (InvalidOperationException ex)
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

        private string GetFileInfo(string path)
        {
            FileInfo fileInfo = new FileInfo(path);
            Icon icon = Icon.ExtractAssociatedIcon(path);

            string base64Icon;
            using (MemoryStream stream= new MemoryStream())
            {
                icon.Save(stream);
                base64Icon = Convert.ToBase64String(stream.ToArray());

            }
            string fileName = fileInfo.Name.ToString();
            string fileSize = fileInfo.Length.ToString();
            return new StringBuilder()
                .Append(fileName)
                .Append("|")
                .Append(fileSize)
                .Append("|")
                .Append(base64Icon).ToString();
        }
        private void fpnChat_Paint(object sender, PaintEventArgs e)
        {

        }
    }
}
