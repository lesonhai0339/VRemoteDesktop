using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using VRemoteClient.Models.CustomLayouts;
using VRemoteClient.Models.DTOs;
using VRemoteClient.Models.Entities;
using VRemoteClient.Models.Enums;
using VRemoteClient.Services.RemoteDesktopService;
using VRemoteClient.Utils;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace VRemoteClient
{
    public partial class FormChat : Form
    {
        private const int ChunkSize = 8192; // Size of each file chunk in bytes 
        private readonly object _lockObject = new object();
        private ConcurrentDictionary<string, ConnectionInfo> _currentChat;
        private ConcurrentDictionary<string, List<Control>> _chatData;
        private RemoteDesktop _remoteDesktop;
        private ConnectionInfo _connectionInfo;
        private string _filePath = string.Empty;
        private string _tempFileName = string.Empty;
        private string _savePath = string.Empty;
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
            _currentChat = new ConcurrentDictionary<string, ConnectionInfo>();
            _chatData = new ConcurrentDictionary<string, List<Control>>();
            _currentChat.TryAdd(_connectionInfo.SessionId, _connectionInfo);
            ShowConnection();
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
                        _remoteDesktop.SendFileEvent -= SendFileEventHandler;
                    }
                    _remoteDesktop = value;
                    if(_remoteDesktop != null)
                    {
                        _remoteDesktop.ChatMessageEvent += ChatMessageEventHandler;
                        _remoteDesktop.SendFileEvent += SendFileEventHandler;

                    }
                }
            }
        }

        private void SendFileEventHandler(SendFileType type,string sessionId, byte[] arg2)
        {
            if(type == SendFileType.RequestSendFile)
            {
                PartnerRequestSendFile(sessionId, arg2);

                //CustomFileTemplate cs = new CustomFileTemplate(AcceptOrRejectFileSentByPartner);
                //var table = cs.ReceivedFileSentFromPartner(sessionId, arg2);
                //AddElementToLayout(table.Table);
            }
            if(type == SendFileType.AcceptSendFile)
            {
                string id = Encoding.ASCII.GetString(arg2);
                FileInfo fileInfo = FileUtils.GetFileInfo(_filePath);
                long chunkNumber = FileUtils.CalculateChunkNumber(fileInfo.Length, ChunkSize);
                int count = 0;
                while(count < chunkNumber)
                {
                    int offset = count * ChunkSize;
                    byte[] chunkData = FileUtils.GetFileDataByOffset(fileInfo.FullName, offset, ChunkSize);


                    byte[] dataSend = new byte[chunkData.Length + 4]; //4 byte for offset
                    Buffer.BlockCopy(BitConverter.GetBytes(offset), 0, dataSend, 0, 4);
                    Buffer.BlockCopy(chunkData, 0, dataSend, 4, chunkData.Length);
                    AddWork(SocketDataType.FileTransfer, dataSend);
                    Log.ForContext("FileName", "FileSend").Info($"Send chunk with {dataSend.Length}bytes to receiver");
                    count++;
                }


            }
            if (type == SendFileType.FileTransfer)
            {
                int offset = BitConverter.ToInt32(arg2 , 0);
                byte[] data = new byte[arg2.Length - 4];
                Buffer.BlockCopy(arg2, 4, data, 0, arg2.Length - 4);
                string path = Path.Combine(Path.GetTempPath(), _tempFileName);
                Utils.FileUtils.WriteToFile(path, offset, data);
                Log.ForContext("Filename", "FileSend").Info($"Receive chunk with {arg2.Length} from sender");
                // Handle rejection logic here if needed
            }
        }
     
        private void AcceptOrRejectFileSentByPartner(bool flag, string sessionId)
        {
            RemoteDesktop.AcceptOrRejectFileSent(flag, sessionId);
        }
        private void ChatMessageEventHandler(byte[] obj)
        {
            string[] message = Encoding.UTF8.GetString(obj).Split('|');
            AddChatMessage(message[0], message[1]);
        }
        #endregion
        private void ConfigDefaultFormPosition()
        {
            Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;

            this.Location = new Point(
                workingArea.Right - this.Width,
                workingArea.Bottom - this.Height - 120
            );

            fpnChat.FlowDirection = FlowDirection.TopDown;
            fpnChat.WrapContents = false;
            fpnChat.AutoScroll = true;
            fpnChat.BorderStyle = BorderStyle.FixedSingle;
            fpnChat.Padding = new Padding(0, 0, SystemInformation.VerticalScrollBarWidth, 0);


            fpnNumberChatConnection.BorderStyle = BorderStyle.FixedSingle;
            fpnNumberChatConnection.FlowDirection = FlowDirection.TopDown;
            fpnNumberChatConnection.WrapContents = false;

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
            AddChatMessage("Tôi: ", txtChatContent.Text);
            string id = _connectionInfo.Me.Id;
            string data = Utils.StringBuilderUtils.StringBuilderWithSeparator("|", id, txtChatContent.Text);
            AddWork(SocketDataType.Message, Encoding.ASCII.GetBytes(data));
        }
        private void AddElementToLayout(Control control)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<Control>(AddElementToLayout), control);
                return;
            }

            if (control != null)
            {
                fpnChat.Controls.Add(control);
                fpnChat.ScrollControlIntoView(control);
            }
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
            using (var dialog = new OpenFileDialog())
            {
                DialogResult result = dialog.ShowDialog();
                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    string selectedPath = dialog.FileName;
                    _filePath = selectedPath;
                    try
                    {
                        //Stopwatch stopwatch = new Stopwatch();
                        //stopwatch.Start();
                        //string checkSum = FileUtils.CreateFileChecksum(selectedPath);
                        //stopwatch.Stop();
                        //MessageBox.Show("Elapsed time: " + stopwatch.Elapsed.TotalSeconds, "Ex");

                        string data = GetFileInfo(selectedPath);
                        var control = FilePrepareSendToPartner(selectedPath);


                        AddWork(SocketDataType.RequestSendFile, Encoding.ASCII.GetBytes(data));
                        AddElementToLayout(control);
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
        private void AddChatMessage(string userName, string data)
        {
            CustomTableLayout table = new CustomTableLayout(_connectionInfo.Partner.Id ,EventCallback)
              .SetColAndRow(1, 2)
              .SetColumAndRowStyle(new List<ColumnStyle>
              {
                        new ColumnStyle(SizeType.Percent, 100F)
              },
              new List<RowStyle>
              {
                        new RowStyle(SizeType.AutoSize),
                        new RowStyle(SizeType.AutoSize)
              });

            Label name = new Label
            {
                Text = userName,
                Font = new Font("Arial", 10, FontStyle.Bold),
            };
            Label message = new Label
            {
                AutoSize = true,
                Text = data,
            };

            table.AddControl(nameof(name), name, 0, 0);
            table.AddControl(nameof(message), message, 0, 1);

            AddElementToLayout(table.Table);
        }
        private void PartnerRequestSendFile(string sessionId, byte[] data)
        {
            string dataString = Encoding.ASCII.GetString(data);
            string[] array = StringBuilderUtils.StringToStringArrayWithSeparator(dataString);

            Icon icon = FileUtils.GetIconByFileName(array[0]);

            CustomTableLayout table = new CustomTableLayout(_connectionInfo.Partner.Id, EventCallback)
               .SetColAndRow(3, 2)
               .SetColumAndRowStyle(new List<ColumnStyle>
               {
                        new ColumnStyle(SizeType.Percent, 20F),
                        new ColumnStyle(SizeType.Percent, 50F),
                        new ColumnStyle(SizeType.Percent, 30F),
               },
               new List<RowStyle>
               {
                        new RowStyle(SizeType.AutoSize),
                        new RowStyle(SizeType.AutoSize),
               });

            PictureBox fileIcon = new PictureBox { Image = icon.ToBitmap() };
            _tempFileName = array[0];
            Label fileName = new Label
            {
                AutoSize = true,
                Text = StringBuilderUtils.GenerateStringShortcut(array[0], 50)
            };
            Label fileSize = new Label { Text = StringBuilderUtils.GetFileSizeString(long.Parse(array[1])) };
            Button btnSave = new Button
            {
                Name = "btnSave",
                Text = "Save",
                BackColor = Color.White
            };
            Button btnCancel = new Button
            {
                Name = "btnCancel",
                Text = "Cancel",
                BackColor = Color.White
            };

            table.AddControl(nameof(fileIcon), fileIcon, 0, 0, true);
            table.AddControl(nameof(fileName), fileName, 1, 0);
            table.AddControl(nameof(fileSize), fileSize, 1, 1);
            table.AddControl(nameof(btnSave), btnSave, 2, 0);
            table.AddControl(nameof(btnCancel), btnCancel, 2, 1);


            table.RegisterEvent(btnSave, nameof(Click), new EventHandler(table.EventHandler));
            table.RegisterEvent(btnCancel, nameof(Click), new EventHandler(table.EventHandler));

            AddElementToLayout(table.Table);
        }
        private void EventCallback(string id, object sender, EventArgs e)
        {
           if(sender is Button btn)
            {
                if(btn.Name == "btnSave")
                {
                    AddWork(SocketDataType.AcceptSendFile, Encoding.ASCII.GetBytes(RemoteDesktop.OwnerInfo.Id));
                }
            }
        }
        public Control FilePrepareSendToPartner(string path)
        {
            FileInfo fileInfo = new FileInfo(path);
            Icon icon = Icon.ExtractAssociatedIcon(path);

            PictureBox picturebox = new PictureBox();
            picturebox.Image = icon.ToBitmap();

            CustomTableLayout table = new CustomTableLayout(_connectionInfo.Partner.Id, EventCallback)
               .SetColAndRow(3, 2)
               .SetColumAndRowStyle(new List<ColumnStyle>
               {
                        new ColumnStyle(SizeType.Percent, 30F),
                        new ColumnStyle(SizeType.Percent, 50F),
                        new ColumnStyle(SizeType.Percent, 20F),
               },
               new List<RowStyle>
               {
                        new RowStyle(SizeType.AutoSize),
                        new RowStyle(SizeType.AutoSize),
               });

            PictureBox fileIcon = new PictureBox { Image = icon.ToBitmap() };
            Label fileName = new Label
            {
                AutoSize = true,
                Text = Utils.StringBuilderUtils.GenerateStringShortcut(fileInfo.Name, 50),
            };
            Label fileSize = new Label
            {
                Text = Utils.StringBuilderUtils.GetFileSizeString(fileInfo.Length)
            };
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "loading.gif");
            PictureBox loading = new PictureBox
            {
                Image = Image.FromFile(Path.Combine(iconPath)),
                SizeMode = PictureBoxSizeMode.StretchImage,
                Size = new Size(30, 30)
            };

            table.AddControl(nameof(fileIcon), fileIcon, 0, 0, true);
            table.AddControl(nameof(fileName), fileName, 1, 0);
            table.AddControl(nameof(fileSize), fileSize, 1, 1);
            table.AddControl(nameof(loading), loading, 2, 0, true);


            AddElementToLayout(table.Table);

            return table.Table;
        }
        private string GetFileInfo(string path)
        {
            FileInfo fileInfo = new FileInfo(path);
            string fileName = fileInfo.Name.ToString();
            string fileSize = fileInfo.Length.ToString();
            return StringBuilderUtils.StringBuilderWithSeparator("|", fileName, fileSize);
        }
        private void fpnChat_Paint(object sender, PaintEventArgs e)
        {

        }
        public bool AddNewChat(ConnectionInfo info)
        {
            if(!_currentChat.TryGetValue(info.SessionId, out var _))
            {
                _currentChat.TryAdd(info.SessionId, info);
                return true;
            }
            else
            {
                return false;
            }
        }
        private void ShowConnection()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(ShowConnection));
                return;
            }

            foreach(var chat in _currentChat)
            {
                CustomTableLayout table = new CustomTableLayout(_connectionInfo.Partner.Id, EventCallback)
                .SetColAndRow(2, 1)
                .SetStyles(
                    new UIPropertyRegistration(nameof(BorderStyle), BorderStyle.FixedSingle),
                    new UIPropertyRegistration(nameof(BackColor), Color.Yellow)
                )
                .SetColumAndRowStyle(
                    new List<ColumnStyle>
                    {
                        new ColumnStyle(SizeType.Percent, 20F),
                        new ColumnStyle(SizeType.Percent, 80F)
                    },
                    new List<RowStyle>
                    {
                        new RowStyle(SizeType.AutoSize),
                    }
                );
               
                Label lbChat = new Label
                {
                    Text = chat.Value.Partner.ComputerName,
                    Name = chat.Value.SessionId,
                    AutoSize = true
                };
                CustomPanel pnStatus = new CustomPanel();
                pnStatus.Paint += pnStatus.CreateCircle;


                lbChat.Click += ConnectionClickEventHandler;

                table.AddControl(nameof(pnStatus), pnStatus, 0, 0);
                table.AddControl(nameof(lbChat), lbChat, 1, 0);

                table.RegisterEvents(
                    new EventRegistration(pnStatus, nameof(MouseHover), new EventHandler((s, e) =>
                    {
                       table.SetStyle(nameof(BackColor), Color.LightBlue);
                    })),
                    new EventRegistration(pnStatus, nameof(MouseLeave), new EventHandler((s, e) =>
                    {
                        table.SetStyle(nameof(BackColor), Color.Yellow);
                    })),


                    new EventRegistration(lbChat, nameof(MouseHover), new EventHandler((s, e) =>
                    {
                        table.SetStyle(nameof(BackColor), Color.LightBlue);
                    })),
                    new EventRegistration(lbChat, nameof(MouseLeave), new EventHandler((s, e) =>
                    {
                        table.SetStyle(nameof(BackColor), Color.Yellow);
                    })),



                    new EventRegistration(table.Table, nameof(MouseHover), new EventHandler((s, e) =>
                    {
                         table.SetStyle(nameof(BackColor), Color.LightBlue);
                    })),
                    new EventRegistration(table.Table, nameof(MouseLeave), new EventHandler((s, e) =>
                    {
                        table.SetStyle(nameof(BackColor), Color.Yellow);
                    }))

                );

                fpnNumberChatConnection.Controls.Add(table.Table);
            }
        }
        private void ConnectionClickEventHandler(object sender, EventArgs e)
        {
            MessageBox.Show("Connection Clicked: " + ((Label)sender).Name);
        }
    }
}
