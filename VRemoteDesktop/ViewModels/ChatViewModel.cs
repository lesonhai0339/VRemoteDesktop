using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Mail;
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
using static System.Net.WebRequestMethods;
using static VRemoteDesktop.Utils.DefaultValue;

namespace VRemoteDesktop.ViewModels
{
    public class ChatViewModel:IDisposable
    {
        private static readonly Color SelectedColor = Color.LightSkyBlue;
        private static readonly Color DefaultColor = Color.White;
        private readonly object _lock = new object();
        private readonly IChatManager<object> _chatConnections;
        private string _currentConnectionActivate;

        private ConcurrentDictionary<string, FileAttachmentLayout> _attachments;
        private readonly IVChatAttachmentService _chatAttachmentService;

        public event EventHandler<ChatControlAddedEventArgs> AddedEvent;
        public event EventHandler<ChatControlRemoveEventArgs> RemovedEvent;
        public event EventHandler<ChatControlUpdateEventArgs> UpdateEvent;
        public event EventHandler<ChatControlProgressBarUpdateUIEventArgs> ProgressBarEvent;
        public ChatViewModel()
        {
            _attachments = new ConcurrentDictionary<string, FileAttachmentLayout>();
            _chatAttachmentService = new VChatAttachmentService();
            _chatAttachmentService.FileEvent += FileEventHandler;

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
                    if (_chatAttachmentService.ReceivedFileInfo(e.Data, false, out VFileInfo info))
                    {
                        var attachment = new FileAttachmentLayout(info.Id);
                        attachment.AcceptSaveFile += FileReceivedClickEventHandler;
                        attachment.Add(info);
                        _attachments[info.Id] = attachment;
                        AddedEvent?.Invoke(this, new ChatControlAddedEventArgs(ChatControlType.Message, attachment));
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
                    string fileId = Encoding.ASCII.GetString(e.Data, 1, 16);
                    if (_attachments.TryGetValue(fileId, out var attachment))
                    {
                        if (respondType == SendFileRespondType.Accept)
                        {
                            UpdateEvent?.Invoke(this, new ChatControlUpdateEventArgs(ChatControlType.Message, () => attachment.UpdateRequestSendFileStatus("Đối tác đã chấp nhận")));
                            _chatAttachmentService.SendFile(client, fileId);
                        }
                        else if (respondType == SendFileRespondType.Reject)
                        {
                            UpdateEvent?.Invoke(this, new ChatControlUpdateEventArgs(ChatControlType.Message, () => attachment.UpdateRequestSendFileStatus("Đối tác đã từ chối")));
                        }
                        else
                        {
                            throw new InvalidOperationException("Unexpected type");
                        }
                    }
                    else
                    {
                        //TODO: Cannot find attachment with id
                    }
                }
                else if (e.Type == DataType.FileTransfer)
                {
                    try
                    {
                        _chatAttachmentService.ProcessFileDataReceived(e.Data);   
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
                    _chatAttachmentService.UpdateFileSavePath(parent.Id, e.FilePath);
                    var client = _chatConnections.GetClientById(_currentConnectionActivate);
                    if (client != null)
                    {
                        var fileId = Encoding.ASCII.GetBytes(parent.Id);
                        byte[] data = new byte[fileId.Length + 1];
                        data[0] = (byte)SendFileRespondType.Accept;
                        Buffer.BlockCopy(fileId, 0 , data , 1 , fileId.Length); 
                        client.AddWork(new TaskObject
                        {
                            TaskType = DataType.AcceptSendFile,
                            Data = data,
                            IsSendHeader = true,
                            SessionId = client.SocketId
                        });
                        UpdateEvent?.Invoke(this, new ChatControlUpdateEventArgs(ChatControlType.Message, () => parent.AcceptSendFile()));
                    }
                }
                //Reject file
                if (string.Compare(btn.Name, "btnCancel") == 0)
                {
                    var fileId = Encoding.ASCII.GetBytes(parent.Id);
                    byte[] data = new byte[fileId.Length + 1];
                    data[0] = (byte)SendFileRespondType.Reject;
                    Buffer.BlockCopy(fileId, 0, data, 1, fileId.Length);
                    var client = _chatConnections.GetClientById(_currentConnectionActivate);
                    if (client != null)
                    {
                        client.AddWork(new TaskObject
                        {
                            TaskType = DataType.AcceptSendFile,
                            Data = data,
                            IsSendHeader = true,
                            SessionId = client.SocketId
                        });
                        UpdateEvent?.Invoke(this, new ChatControlUpdateEventArgs(ChatControlType.Message, () => parent.RejectSendFile()));
                    }
                    _attachments.TryRemove(parent.Id, out _);
                    _chatAttachmentService.RemoveFileInfo(parent.Id);
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
            if(sender is Label lb && lb.Parent is FlowLayoutPanel flow)
            {
                foreach(var connection in flow.Controls.OfType<Label>())
                {
                    if(connection == lb)
                    {
                        _currentConnectionActivate = lb.Name;
                        lb.BackColor = SelectedColor;
                    }
                    else
                    {
                        connection.BackColor = DefaultColor;
                    }
                }
            }
        }
        //Create new label and send message to partner
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
        //Create new file info and send request to partner
        public void RequestSendFile()
        {
            try
            {
                var fileInfo = _chatAttachmentService.GetFileSendInfo();
                if (fileInfo != null)
                {
                    FileAttachmentLayout fileAttachmentLayout = new FileAttachmentLayout(fileInfo.Id);
                    fileAttachmentLayout.Add(fileInfo, true);
                    _attachments.TryAdd(fileInfo.Id, fileAttachmentLayout);

                    string data = Helpers.StringHelper.StringBuilderWithSeparator("|", fileInfo.Id, fileInfo.Filename, fileInfo.FileExtension, fileInfo.FileSize);
                    byte[] byteArray = Helpers.ByteArrayHelper.ConvertStringToByteArray(data, Enums.EncodingType.UTF8).GetResult();
                    SendToClient(DataType.RequestSendFile, byteArray);

                    AddedEvent?.Invoke(this, new ChatControlAddedEventArgs(ChatControlType.Message, fileAttachmentLayout));
                }
            }
            catch(Exception ex)
            {
                throw;
            }
        }
        //Send data to current activated connection(VClient)
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
                    SessionId = client.SocketId,
                    Priority = QueuePriority.High
                });
            }
            else
            {
                throw new InvalidOperationException("Cannot find client with id: "+ _currentConnectionActivate);
            }
        }
        //Event event handler from ChatManager, this event is used to remove chat connection UI when disconnected
        private void ChatDisconnectedEventHandler(object sender, ChatDisconnectedEventArgs e)
        {
            RemovedEvent?.Invoke(this, new ChatControlRemoveEventArgs(ChatControlType.Connection, e.SocketId));
        }
        //Event event handler from FileService, this event is used to update progress bar UI when received file data, finally remove file out attachments when finished
        private void FileEventHandler(object sender, FileEventArgs e)
        {
            if (_attachments.TryGetValue(e.FileId, out var attachment))
            {
                ProgressBarEvent?.Invoke(this, new ChatControlProgressBarUpdateUIEventArgs(attachment, e.Size));
                if (e.Status == FileStatus.Finished)
                    _attachments.TryRemove(e.FileId, out _);
            }
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
                if(_chatAttachmentService != null)
                {
                    _chatAttachmentService.FileEvent -= FileEventHandler;
                    _chatAttachmentService.Dispose();
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
