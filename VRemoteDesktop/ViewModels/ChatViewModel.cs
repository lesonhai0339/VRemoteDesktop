using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Layouts;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.FileService;
using VRemoteDesktop.Services.VTCPClient;
using static VRemoteDesktop.Utils.DefaultValue;

namespace VRemoteDesktop.ViewModels
{
    public class ChatViewModel:IDisposable
    {
        private readonly object _lock = new object();
        private readonly IChatManager<object> _chatConnections;
        private string _currentConnectionActivate;
        private FileAttachmentLayout _currentFileAttachment;
        private readonly IVFileExtension _fileExtension;

        public event EventHandler<ChatControlAddedEventArgs> AddedEvent;
        public event EventHandler<ChatControlRemoveEventArgs> RemovedEvent;
        public event EventHandler<ChatControlUpdateEventArgs> UpdateEvent;
        public event EventHandler<ChatControlProgressBarUpdateUIEventArgs> ProgressBarEvent;
        public ChatViewModel()
        {
            _fileExtension = new VFileExtension();
            _fileExtension.FileEvent += FileEventHandler;
            _chatConnections = new ChatManager<object>();
            _chatConnections.ChatDisconnected += ChatDisconnectedEventHandler;
        }
        public void AddConnection(string key, VClient client)
        {
            _chatConnections.Add(key, client);
            client.P2PChatReceived += P2PChatReceivedEventHandler;
            _currentConnectionActivate = key;
            AddChatConnection(key);
        }
        public void RemoveConnection(string key)
        {
            var client = _chatConnections.GetClientById(key);
            if(client != null)
            {
                client.P2PChatReceived -= P2PChatReceivedEventHandler;
                _chatConnections.Remove(key);
                _currentConnectionActivate = _chatConnections.GetLastConnectionId();
                RemovedEvent?.Invoke(this, new ChatControlRemoveEventArgs(ChatControlType.Connection, key));
            }
        }
        private void P2PChatReceivedEventHandler(object sender, P2PChatEventArgs e)
        {
            if(sender is VClient client)
            {
                if (e.Type == DataType.RequestSendFile)
                {
                    if (_fileExtension.AddFileInfo(e.Data, false, out VFileInfo info))
                    {
                        _currentFileAttachment = new FileAttachmentLayout();
                        _currentFileAttachment.AcceptSaveFile += FileReceivedClickEventHandler;
                        _currentFileAttachment.Add(info);
                        AddedEvent?.Invoke(this, new ChatControlAddedEventArgs(ChatControlType.Message, _currentFileAttachment));
                    }
                    else
                    {
                        throw new InvalidOperationException("Error when received request send file");
                    }
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
                    AddedEvent?.Invoke(this, new ChatControlAddedEventArgs(ChatControlType.Message, lb));
                }
                else if (e.Type == DataType.AcceptSendFile)
                {
                    SendFileRespondType respondType = (SendFileRespondType)e.Data[0];
                    if (respondType == SendFileRespondType.Accept)
                    {
                        UpdateEvent?.Invoke(this, new ChatControlUpdateEventArgs(ChatControlType.Message, ()=> _currentFileAttachment.UpdateRequestSendFileStatus("Đối tác đã chấp nhận")));
                        _fileExtension.SendFile(client);
                    }
                    else if(respondType == SendFileRespondType.Reject)
                    {
                        UpdateEvent?.Invoke(this, new ChatControlUpdateEventArgs(ChatControlType.Message, () => _currentFileAttachment.UpdateRequestSendFileStatus("Đối tác đã từ chối")));
                    }
                    else
                    {
                        throw new InvalidOperationException("Unexpected type");
                    }
                }
                else if (e.Type == DataType.FileTransfer)
                {
                    try
                    {
                        _ = Task.Factory.StartNew(() =>
                        {
                            _fileExtension.WriteData(e.Data);
                        });
                        //remove 4 byte header
                        ProgressBarEvent?.Invoke(this, new ChatControlProgressBarUpdateUIEventArgs(_currentFileAttachment, e.Data.Length - 4));
                    }
                    catch(Exception ex)
                    {
                        throw ex;
                    }
                }
            }
        }
        public void FileReceivedClickEventHandler(object sender, P2PFileReceivedEventArgs e)
        {
            if(sender is Button btn && btn.Parent is FileAttachmentLayout parent)
            {
                //Accept file
                if (string.Compare(btn.Name, "btnSave") == 0)
                {
                    _fileExtension.UpdateSavePath(e.FilePath);

                    var client = _chatConnections.GetClientById(_currentConnectionActivate);
                    if (client != null)
                    {
                        client.AddWork(new TaskObject
                        {
                            TaskType = DataType.AcceptSendFile,
                            Data = new byte[] { (byte)SendFileRespondType.Accept },
                            IsSendHeader = true,
                            SessionId = client.SocketId
                        });
                        UpdateEvent?.Invoke(this, new ChatControlUpdateEventArgs(ChatControlType.Message, () => parent.AcceptSendFile()));
                    }
                }
                //Reject file
                if (string.Compare(btn.Name, "btnCancel") == 0)
                {
                    _fileExtension.Clear();
                    var client = _chatConnections.GetClientById(_currentConnectionActivate);
                    if (client != null)
                    {
                        client.AddWork(new TaskObject
                        {
                            TaskType = DataType.AcceptSendFile,
                            Data = new byte[] { (byte)SendFileRespondType.Reject },
                            IsSendHeader = true,
                            SessionId = client.SocketId
                        });
                        UpdateEvent?.Invoke(this, new ChatControlUpdateEventArgs(ChatControlType.Message, () => parent.RejectSendFile()));
                    }
                }
            }
        }
        public void AddChatConnection(string id)
        {
            var client = _chatConnections.GetClientById(_currentConnectionActivate);
            if (client != null)
            {
                Label lbChat = new Label
                {
                    Text = client.Partner.ComputerName,
                    Name = client.SocketId,
                    BackColor = Color.WhiteSmoke,
                    BorderStyle = BorderStyle.FixedSingle,
                    AutoSize = false,
                    Height = 20,
                    Margin = Padding.Empty
                };
                lbChat.Click += ChangeConnectionActivate;
                AddedEvent?.Invoke(this, new ChatControlAddedEventArgs(ChatControlType.Connection, lbChat));
            }
        }
        public void ChangeConnectionActivate(object sender, EventArgs e)
        {
            if(sender is Label lb)
            {
                _currentConnectionActivate = lb.Name;
                lb.BackColor = Color.LightGray;
            }
        }
        public void SendChatMessage(string chatData)
        {
            SendToClient(DataType.Message, Encoding.ASCII.GetBytes(chatData));
            Label lb = new Label
            {
                Text = "Me: " + chatData,
                AutoSize = true,
                TextAlign = ContentAlignment.TopLeft,
            };
            AddedEvent?.Invoke(this, new ChatControlAddedEventArgs(ChatControlType.Message, lb));
        }
        public void RequestSendFile()
        {
            var fileInfo = _fileExtension.GetFileSendInfo();
            if(fileInfo != null)
            {
                FileAttachmentLayout fileAttachmentLayout = new FileAttachmentLayout();
                fileAttachmentLayout.Add(fileInfo, true);
                _currentFileAttachment = fileAttachmentLayout;

                string data = Helpers.StringHelper.StringBuilderWithSeparator("|", fileInfo.FileExtension, fileInfo.Filename, fileInfo.FileSize);
                byte[] byteArray = Helpers.ByteArrayHelper.ConvertStringToByteArray(data, Enums.EncodingType.UTF8).GetResult();
                SendToClient(DataType.RequestSendFile, byteArray);

                AddedEvent?.Invoke(this, new ChatControlAddedEventArgs(ChatControlType.Message, fileAttachmentLayout));
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
        private void ChatDisconnectedEventHandler(object sender, ChatDisconnectedEventArgs e)
        {
            RemovedEvent?.Invoke(this, new ChatControlRemoveEventArgs(ChatControlType.Connection, e.SocketId));
        }
        private void FileEventHandler(object sender, FileEventArgs e)
        {
            Console.WriteLine("Finished write file");
            _fileExtension.Clear();
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
                if(_fileExtension != null)
                {
                    _fileExtension.FileEvent -= FileEventHandler;
                    _fileExtension.Dispose();
                }
                if(_chatConnections != null)
                {
                    _chatConnections.ChatDisconnected -= ChatDisconnectedEventHandler;
                    _chatConnections.Dispose();
                }
            }
        }
    }
}
