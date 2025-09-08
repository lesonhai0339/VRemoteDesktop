using System;
using System.IO;
using System.Reflection;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.FileService;
using VRemoteDesktop.Services.VTCPClient;
using static System.Net.WebRequestMethods;
using static VRemoteDesktop.Utils.DefaultValue;

namespace VRemoteDesktop.ViewModels
{
    public class ChatViewModel:IDisposable
    {
        private readonly object _lock = new object();
        private readonly IChatManager<object> _chatConnections;
        private string _currentConnectionActivate;

        private readonly ISaveChat _saveChat;
        private readonly IVChatAttachmentService _chatAttachmentService;

        public event EventHandler<ChatControlAddedEventArgs> AddedEvent;
        public event EventHandler<ChatControlRemoveEventArgs> RemovedEvent;
        public event EventHandler<ChatControlUpdateEventArgs> UpdateEvent;
        public event EventHandler<ChatControlProgressBarUpdateUIEventArgs> ProgressBarEvent;
        public event EventHandler<ChatUpdateChatHistoryEventArgs> UpdateChatHistoryEvent;
        public event EventHandler<P2PFileReceivedEventArgs> FileClickedEvent;
        public event EventHandler<ChatErrorEventArgs> ErrorEvent;
        public ChatViewModel()
        {
            _saveChat = new SaveChat(); 
            _chatAttachmentService = new VChatAttachmentService();
            _chatAttachmentService.FileEvent += FileEventHandler;

            _chatConnections = new ChatManager<object>();
            _chatConnections.ChatDisconnected += ChatDisconnectedEventHandler;
        }
        #region Properties
        #endregion
        #region Methods
        public void AddConnection(string connectionId, VClient connection)
        {
            if(!_chatConnections.Add(connectionId, connection))
            {

                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new InvalidOperationException(string.Format("Cannot add connection with id {0}", connectionId))));
                return;
            }
            connection.P2PChatReceived += P2PChatReceivedEventHandler;
            _currentConnectionActivate = connectionId;
            AddChatConnection(connectionId);
        }
        public void RemoveConnection(string connectionId)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new ArgumentNullException(nameof(connectionId), "Missing connectionId")));
                return;
            }
            var connection = _chatConnections.GetClientById(connectionId);
            if (connection == null)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new InvalidOperationException(string.Format("Connection with id {0} does not exists", connectionId))));
                return;  
            }
            connection.P2PChatReceived -= P2PChatReceivedEventHandler;
            _chatConnections.Remove(connectionId);
            _currentConnectionActivate = _chatConnections.GetLastConnectionId();
            RemovedEvent?.Invoke(this, new ChatControlRemoveEventArgs(ChatControlType.Connection, connectionId));
        }
        public void AddChatConnection(string connectionId)
        {
            var connection = _chatConnections.GetClientById(connectionId);
            if(connection == null)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new InvalidOperationException(string.Format("Connection with id {0} does not exists", connectionId))));
                return;
            }
            AddedEvent?.Invoke(this, new ChatControlAddedEventArgs(ChatControlType.Connection, connection.SocketId, connection.Partner.ComputerName, null));
        }
        public string GetConnectionNameById(string connectionId)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new ArgumentNullException(nameof(connectionId), "Missing connectionId")));
                return string.Empty;
            }
            var connection = _chatConnections.GetClientById(connectionId);
            if (connection == null)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new InvalidOperationException(string.Format("Does not exists connection with id {0}", connectionId))));
                return string.Empty;
            }
            return connection.Partner.ComputerName;
        }
        public bool IsValidConnection(string connectionId)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new ArgumentNullException(nameof(connectionId), "Missing connectionId")));
                return false;
            }
            return _chatConnections.ContainsKey(connectionId);
        }
        public void SetCurrentConnectionActivate(string connectionId)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new ArgumentNullException(nameof(connectionId), "Missing connectionId")));
                return;
            }
            _currentConnectionActivate = connectionId;
        }
        public void UpdateFileSavePath(string fileId, string filePath)
        {
            if (string.IsNullOrWhiteSpace(fileId))
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new ArgumentNullException(nameof(fileId), "Missing fileId")));
                return;
            }
            if (string.IsNullOrWhiteSpace(filePath))
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new ArgumentNullException(nameof(filePath), "Missing filePath")));
                return;
            }
            _chatAttachmentService.UpdateFileSavePath(fileId, filePath);
        }
        public void SaveFileChat(string connectionId, string savePath, string fileName, long fileSize)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new ArgumentNullException(nameof(connectionId), "Missing connectionId")));
                return;
            }
            if (string.IsNullOrWhiteSpace(savePath))
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new ArgumentNullException(nameof(savePath), "Missing savePath")));
                return;
            }
            if (string.IsNullOrWhiteSpace(fileName))
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new ArgumentNullException(nameof(fileName), "Missing fileName")));
                return;
            }
            if (fileSize <= 0)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new ArgumentOutOfRangeException(nameof(fileSize), "fileSize must be greater than 0")));
                return;
            }
            SaveChatFile(connectionId, ChatContentTypeEnum.File, ChatOwnerEnum.Partner, savePath, fileName, fileSize);
        }
        public bool ProcessAcceptSendFile(string fileId)
        {
            try
            {
                if(string.IsNullOrWhiteSpace(fileId))
                {
                    ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new ArgumentNullException(nameof(fileId), "Missing fileId")));
                    return false;
                }    

                var connection = _chatConnections.GetClientById(_currentConnectionActivate);
                if (connection == null)
                {
                    ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new InvalidOperationException(string.Format("Does not exists connection with id {0}", _currentConnectionActivate))));
                    return false;
                }   

                byte[] fileIdArray = Helpers.ByteArrayHelper.ConvertStringToByteArray(fileId, EncodingType.ASCII).GetResult();
                byte[] data = new byte[fileIdArray.Length + 1];
                data[0] = (byte)SendFileRespondType.Accept;
                Buffer.BlockCopy(fileIdArray, 0, data, 1, fileIdArray.Length);
                connection.AddWork(new TaskObject
                {
                    TaskType = DataType.AcceptSendFile,
                    Data = data,
                    IsSendHeader = true,
                    SessionId = connection.SocketId,
                    Priority = QueuePriority.High
                });
                return true;
            }
            catch(Exception ex)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, ex));
                return false;
            }
        }
        public bool ProcessRejectSendFile(string fileId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fileId))
                {
                    ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new ArgumentNullException(nameof(fileId), "Missing fileId")));
                    return false;
                }

                byte[] fileIdArray = Helpers.ByteArrayHelper.ConvertStringToByteArray(fileId, EncodingType.ASCII).GetResult();
                byte[] data = new byte[fileIdArray.Length + 1];
                data[0] = (byte)SendFileRespondType.Reject;
                Buffer.BlockCopy(fileIdArray, 0, data, 1, fileIdArray.Length);
                var connection = _chatConnections.GetClientById(_currentConnectionActivate);
                if (connection == null)
                {
                    ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new InvalidOperationException(string.Format("Does not exists connection with id {0}", _currentConnectionActivate))));
                    return false;
                }
                connection.AddWork(new TaskObject
                {
                    TaskType = DataType.AcceptSendFile,
                    Data = data,
                    IsSendHeader = true,
                    SessionId = connection.SocketId,
                    Priority = QueuePriority.High
                });
                _chatAttachmentService.RemoveFileInfo(fileId);
                return true;
            }
            catch(Exception ex)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, ex));
                return false; 
            }
        }
        public string GetCurrentConnectionActivate()
        {
            return _currentConnectionActivate;
        }
        //Create new label and send message to partner
        public void SendChatMessage(string chatData)
        {
            try
            {
                SendToClient(DataType.Message, Helpers.ByteArrayHelper.ConvertStringToByteArray(chatData, EncodingType.UTF8).GetResult());
                SaveChatText(_currentConnectionActivate, ChatContentTypeEnum.Message, ChatOwnerEnum.Me, chatData);
                AddedEvent?.Invoke(this, new ChatControlAddedEventArgs(ChatControlType.Message, _currentConnectionActivate, chatData, null));
            }
            catch(Exception ex)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, ex));
                return;
            }
        }
        //Create new file info and send request to partner
        public void RequestSendFile()
        {
            try
            {
                var fileInfo = _chatAttachmentService.GetFileSendInfo();
                if(fileInfo == null)
                {
                    ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new ArgumentException("Cannot get fileinfo")));
                    return;
                }

                string data = Helpers.StringHelper.StringBuilderWithSeparator("|", fileInfo.Id, fileInfo.Filename, fileInfo.FileExtension, fileInfo.FileSize);
                byte[] byteArray = Helpers.ByteArrayHelper.ConvertStringToByteArray(data, Enums.EncodingType.UTF8).GetResult();
                SendToClient(DataType.RequestSendFile, byteArray);

                //Write to chat file
                SaveChatFile(_currentConnectionActivate, ChatContentTypeEnum.File, ChatOwnerEnum.Me, fileInfo.FilePath, fileInfo.Filename, fileInfo.FileSize);

                AddedEvent?.Invoke(this, new ChatControlAddedEventArgs(ChatControlType.RequestAttachment, _currentConnectionActivate, null, fileInfo));
            }
            catch (Exception ex)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, ex));
                return;
            }
        }
        private void SaveChatText(string id, ChatContentTypeEnum type, ChatOwnerEnum owner, string message)
        {
            string savePath = GetChatPath(id);
            if(string.IsNullOrWhiteSpace(savePath))
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new ArgumentException("Save path is null")));
                return;
            }
            _saveChat.Add(
                new ChatMessage(savePath: savePath,
                    new ChatText(type, owner, message, DateTime.Now)
                )
            );
        }
        private void SaveChatFile(string id, ChatContentTypeEnum type, ChatOwnerEnum owner, string filePath, string fileName, long fileSize)
        {
            string savePath = GetChatPath(id);
            if (string.IsNullOrWhiteSpace(savePath))
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new ArgumentException("Save path is null")));
                return;
            }
            _saveChat.Add(
                new ChatMessage( savePath: savePath,
                    new ChatFile(type, owner, filePath,fileName, fileSize, DateTime.Now)
                )
            );
        }
        public void LoadChatHistoryByConnectionId(string connectionId)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new ArgumentException("connectionId is null")));
                return;
            }
            string chatHistoryFilePath = GetChatPath(connectionId);
            if (string.IsNullOrWhiteSpace(chatHistoryFilePath))
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Warning, new ArgumentException("ChatHistoryFilePath path is null")));
                return;
            }
            object[] messages = _saveChat.ReadLastMessagesObject(chatHistoryFilePath, 5);
            if (messages == null || messages.Length == 0)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Warning, new ArgumentException("messages is null")));
                return;
            }
            UpdateChatHistoryEvent?.Invoke(this, new ChatUpdateChatHistoryEventArgs(ChatUpdateChatHistoryEventType.LoadHistory, connectionId, messages));
        }
        private string GetChatPath(string connectionId)
        {
            try
            {
                var connection = _chatConnections.GetClientById(connectionId);
                if (connection == null)
                {
                    ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new InvalidOperationException(string.Format("Does not exists connection with id {0}", connectionId))));
                    return string.Empty;
                }
                string savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DEFAULT_CHAT_FOLDER, connection.Partner.ComputerName + ".txt");
                return savePath;
            }
            catch(Exception ex)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, ex));
                return string.Empty;
            }
        }
        //Send data to current activated connection(VClient)
        private void SendToClient(DataType type, byte[] data)
        {
            try
            {
                if (string.IsNullOrEmpty(_currentConnectionActivate))
                {
                    _currentConnectionActivate = _chatConnections.GetLastConnectionId();
                }
                var connection = _chatConnections.GetClientById(_currentConnectionActivate);
                if (connection == null)
                {
                    ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new InvalidOperationException(string.Format("Does not exists connection with id {0}", _currentConnectionActivate))));
                    return;
                }
                connection.AddWork(new TaskObject
                {
                    TaskType = type,
                    Data = data,
                    IsSendHeader = true,
                    SessionId = connection.SocketId,
                    Priority = QueuePriority.High
                });
            }
            catch(Exception ex)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, ex));
                return;
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
                if (_chatAttachmentService != null)
                {
                    _chatAttachmentService.FileEvent -= FileEventHandler;
                    _chatAttachmentService.Dispose();
                }
                if (_chatConnections != null)
                {
                    _chatConnections.ChatDisconnected -= ChatDisconnectedEventHandler;
                    _chatConnections.Dispose();
                }
            }
        }
        #endregion
        #region Events
        //Event event handler from ChatManager, this event is used to remove chat connection UI when disconnected
        private void ChatDisconnectedEventHandler(object sender, ChatDisconnectedEventArgs e)
        {
            RemovedEvent?.Invoke(this, new ChatControlRemoveEventArgs(ChatControlType.Connection, e.SocketId));
        }
        //Event event handler from FileService, this event is used to update progress bar UI when received file data, finally remove file out attachments when finished
        private void FileEventHandler(object sender, FileEventArgs e)
        {
            ProgressBarEvent?.Invoke(this, new ChatControlProgressBarUpdateUIEventArgs(e.FileId, e.Size, e.Status));
        }
        private void P2PChatReceivedEventHandler(object sender, P2PChatEventArgs e)
        {
            try
            {
                if (sender is VClient client)
                {
                    if (e.Type == DataType.RequestSendFile)
                    {
                        if (!_chatAttachmentService.ReceivedFileInfo(e.Data, false, out VFileInfo info))
                        {
                            ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new InvalidOperationException("Error when received request send file")));
                            return;
                        }
                        AddedEvent?.Invoke(this, new ChatControlAddedEventArgs(ChatControlType.ReceivedAttachment, client.SocketId, null, info));
                    }
                    else if (e.Type == DataType.Message)
                    {
                        string data = Helpers.ByteArrayHelper.ConvertByteArrayToString(e.Data, Enums.EncodingType.UTF8).GetResult();
                        SaveChatText(client.SocketId, ChatContentTypeEnum.Message, ChatOwnerEnum.Partner, data);
                        AddedEvent?.Invoke(this, new ChatControlAddedEventArgs(ChatControlType.Message, client.SocketId, data, null));
                    }
                    else if (e.Type == DataType.AcceptSendFile)
                    {
                        SendFileRespondType respondType = (SendFileRespondType)e.Data[0];

                        string fileId = Helpers.ByteArrayHelper.ConvertByteArrayToString(e.Data, 1, 16, EncodingType.ASCII).GetResult();
                        if (string.IsNullOrWhiteSpace(fileId))
                        {
                            ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new InvalidOperationException("FileId is null or empty")));
                            return;
                        }
                        if (respondType == SendFileRespondType.Accept)
                        {
                            UpdateEvent?.Invoke(this, new ChatControlUpdateEventArgs(ChatControlType.AcceptAttachment, fileId));
                            _chatAttachmentService.SendFile(client, fileId);
                        }
                        if (respondType == SendFileRespondType.Reject)
                        {
                            UpdateEvent?.Invoke(this, new ChatControlUpdateEventArgs(ChatControlType.RejectAttachment, fileId));
                        }
                    }
                    else if (e.Type == DataType.FileTransfer)
                    {
                        try
                        {
                            _chatAttachmentService.ProcessFileDataReceived(e.Data);
                        }
                        catch (Exception ex)
                        {
                            ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, ex));
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, ex));
            }
        }
        public void FileReceivedClickEventHandler(object sender, P2PFileReceivedEventArgs e)
        {
            FileClickedEvent?.Invoke(sender, e);
        }
        #endregion
    }
}
