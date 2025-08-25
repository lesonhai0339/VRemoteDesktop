using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.Events;
using VRemoteDesktop.Layouts;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.VTCPClient;
using static System.Windows.Forms.LinkLabel;

namespace VRemoteDesktop.ViewModels
{
    public class ChatConnection
    {
        public ChatConnection(VClient client , List<object> lists)
        {
            Client = client;
            Messages = lists;
        }
        public VClient Client { get; set; }
        public List<object> Messages { get; set; }
    }
    public class ChatViewModel: INotifyPropertyChanged, IDisposable
    {
        private readonly IChatManager<VClient, object> _chatConnections;
        private string _clientAdded;
        private string _clientRemoved;
        private string _currentConnectionActivate;
        private string _filePath;
        public event Action<Control> ControlEvent;
        public ChatViewModel()
        {
            _chatConnections = new ChatManager<VClient, object>();
        }
        public string ClientAdded
        {
            get => _clientAdded;
            private set
            {
                _clientAdded = value;
                OnPropertyChanged("ClientAdded");
            }
        }
        public string ClientRemoved
        {
            get => _clientRemoved;
            private set
            {
                _clientAdded = value;
                OnPropertyChanged("ClientRemoved");
            }
        }
        public void UpdateConnection(string key, VClient client)
        {
            _chatConnections.Add(key, client);
            client.P2PChatReceived += P2PChatReceivedEventHandler;
            _currentConnectionActivate = key;
            ClientAdded = key;
        }
        public void RemoveConnection(string key)
        {
            var client = _chatConnections.GetClientById(key);
            if(client != null)
            {
                client.P2PChatReceived -= P2PChatReceivedEventHandler;
                _chatConnections.Remove(key);
                _currentConnectionActivate = _chatConnections.GetLastConnectionId();
                ClientRemoved = key;
            }
        }

        private void P2PChatReceivedEventHandler(object sender, P2PChatEventArgs e)
        {
            if(sender is VClient client)
            {
                if (e.Type == DataType.RequestSendFile)
                {
                    string[] data = Helpers.StringHelper.StringToStringArrayWithSeparator(Encoding.UTF8.GetString(e.Data), "|");
                    FileReceived file = new FileReceived();
                    file.Add(new FileReceivedInfo
                    {
                        FileExtension = data[0],
                        Filename = data[1],
                        FileSize = long.Parse(data[2])
                    });
                    file.ClickedEvent += FileReceivedClickEventHandler;
                    ControlEvent?.Invoke(file);
                }
                else if (e.Type == DataType.Message)
                {
                    string data = Helpers.ByteArrayHelper.ConvertByteArrayToString(e.Data, Enums.EncodingType.UTF8).GetResult();
                    Label lb = new Label
                    {
                        Text = client.Partner.ComputerName + ": " + data,
                        AutoSize = true,
                        TextAlign = ContentAlignment.TopLeft,
                    };
                    ControlEvent?.Invoke(lb);

                }
                else if (e.Type == DataType.AcceptSendFile)
                {
                    int flag = e.Data[0];
                    if (flag == 1)
                    {
                        MessageBox.Show("Partner accepted send file");
                    }
                    else
                    {
                        MessageBox.Show("Partner Rejected send file");
                    }
                }
                else if (e.Type == DataType.FileTransfer)
                {
                    
                }
            }
        }
        private void FileReceivedClickEventHandler(object sender, EventArgs e)
        {
            if(sender is Button btn && btn.Parent is FileReceived pr)
            {
                //Accept file
                if (string.Compare(btn.Name, "btnSave") == 0)
                {
                    var client = _chatConnections.GetClientById(_currentConnectionActivate);
                    if (client != null)
                    {
                        client.AddWork(new TaskObject
                        {
                            TaskType = DataType.AcceptSendFile,
                            Data = new byte[] { 1 },
                            IsSendHeader = true,
                            SessionId = client.SocketId
                        });
                        pr.RemoveButton("Accepted");
                    }
                }
                //Reject file
                if (string.Compare(btn.Name, "btnCancel") == 0)
                {
                    var client = _chatConnections.GetClientById(_currentConnectionActivate);
                    if (client != null)
                    {
                        client.AddWork(new TaskObject
                        {
                            TaskType = DataType.AcceptSendFile,
                            Data = new byte[] { 0 },
                            IsSendHeader = true,
                            SessionId = client.SocketId
                        });
                        pr.RemoveButton("Rejected");
                    }
                }
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public Label NewControl(string id)
        {
            var client = _chatConnections.GetClientById(_currentConnectionActivate);
            if (client != null)
            {
                Label lbChat = new Label
                {
                    Text = client.Partner.ComputerName,
                    Name = client.SocketId,
                    BackColor = Color.WhiteSmoke,
                    BorderStyle = BorderStyle.FixedSingle
                };
                lbChat.Click += ChangeConnectionActivate;
                lbChat.MouseHover += (s, e) =>
                {
                    lbChat.BackColor = Color.Aqua;
                    lbChat.Cursor = Cursors.Hand;
                };
                lbChat.MouseLeave += (s, e) =>
                {
                    lbChat.BackColor = Color.WhiteSmoke;
                    lbChat.Cursor = Cursors.Default;
                };
                return lbChat;
            }
            return null;
        }
        private void ChangeConnectionActivate(object sender, EventArgs e)
        {
            if(sender is Label lb)
            {
                _currentConnectionActivate = lb.Name;
            }
        }
        public void SendChatMessage(string chatData)
        {
            SendToClient(DataType.Message, Encoding.ASCII.GetBytes(chatData));
        }
        public void RequestSendFile()
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
                        FileInfo fileInfo = Helpers.FileHelper.GetFileInfo(_filePath);
                        if (fileInfo != null)
                        {
                            FileReceived file = new FileReceived();
                            file.Add(new FileReceivedInfo
                            {
                                FileExtension = fileInfo.Extension,
                                Filename = fileInfo.Name,
                                FileSize = fileInfo.Length
                            });

                            string data = Helpers.StringHelper.StringBuilderWithSeparator("|", fileInfo.Extension, fileInfo.Name, fileInfo.Length);
                            byte[] byteArray = Helpers.ByteArrayHelper.ConvertStringToByteArray(data, Enums.EncodingType.UTF8).GetResult();
                            SendToClient(DataType.RequestSendFile, byteArray);

                            ControlEvent?.Invoke(file);
                        }
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
        private void SendToClient(DataType type, byte[] data)
        {
            if (string.IsNullOrEmpty(_currentConnectionActivate))
            {
                _currentConnectionActivate = _chatConnections.GetLastConnectionId();
            }
            var client = _chatConnections.GetClientById(_currentConnectionActivate);
            if (client != null)
            {
                client.AddWork(new TaskObject
                {
                    TaskType = type,
                    Data = data,
                    IsSendHeader = true,
                    SessionId = client.SocketId
                });
            }
            else
            {
                throw new InvalidOperationException("Cannot find client with id: "+ _currentConnectionActivate);
            }
        }
        public void EventCallback(object sender, EventArgs e)
        {
            MessageBox.Show(" Callback");
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {

            }
        }
 
    }
}
