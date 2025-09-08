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
        public event EventHandler<ChatControlProgressBarUpdateUIEventArgs> ProgressBarUpdateEvent;
        public event EventHandler<ChatUpdateChatHistoryEventArgs> UpdateChatHistoryEvent;
        public event EventHandler<ChatErrorEventArgs> ErrorEvent;
        public ChatViewModel()
        {
            _saveChat = new SaveChat(); 
            _chatAttachmentService = new VChatAttachmentService();
            _chatAttachmentService.FileDataReceivedEvent += FileDataReceivedEventHandler;

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
            if (!ValidateConnectionId(connectionId))
                return;

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
            if (!ValidateConnectionId(connectionId))
                return string.Empty;

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
            if (!ValidateConnectionId(connectionId))
                return false;

            return _chatConnections.ContainsKey(connectionId);
        }
        public void SetCurrentConnectionActivate(string connectionId)
        {
            if (!ValidateConnectionId(connectionId))
                return;

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
            if (!ValidateConnectionId(connectionId))
                return;

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
        public bool AcceptedFile(string fileId)
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

                byte[] data = Helpers.ByteArrayHelper.ConvertStringToByteArray(fileId, EncodingType.ASCII).GetResult();
                SendToClient(connection, SocketDataType.Chat, ChatDataType.AcceptedSendFile, data);
                return true;
            }
            catch(Exception ex)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, ex));
                return false;
            }
        }
        public bool DeclinedFile(string fileId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fileId))
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
                byte[] data = Helpers.ByteArrayHelper.ConvertStringToByteArray(fileId, EncodingType.ASCII).GetResult();
                SendToClient(connection, SocketDataType.Chat, ChatDataType.DeclinedSendFile, data);  
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
                SendToClient(SocketDataType.Chat, ChatDataType.Message, Helpers.ByteArrayHelper.ConvertStringToByteArray(chatData, EncodingType.UTF8).GetResult());
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

                string data = Helpers.StringHelper.StringBuilderWithSeparator(DEFAULT_SEPRATOR, fileInfo.Id, fileInfo.Filename, fileInfo.FileExtension, fileInfo.FileSize);
                byte[] byteArray = Helpers.ByteArrayHelper.ConvertStringToByteArray(data, Enums.EncodingType.UTF8).GetResult();
                SendToClient(SocketDataType.Chat, ChatDataType.RequestSendFile, byteArray);

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
            if (!ValidateConnectionId(connectionId))
                return;

            string chatHistoryFilePath = GetChatPath(connectionId);
            if (string.IsNullOrWhiteSpace(chatHistoryFilePath))
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Warning, new ArgumentException("ChatHistoryFilePath path is null")));
                return;
            }
            object[] messages = _saveChat.ReadLastMessagesObject(chatHistoryFilePath, DEFAULT_MESSAGE_LOAD);
            if (messages == null || messages.Length == 0)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Warning, new ArgumentException("messages is null")));
                return;
            }
            UpdateChatHistoryEvent?.Invoke(this, new ChatUpdateChatHistoryEventArgs(ChatUpdateChatHistoryEventType.LoadHistory, connectionId, messages));
        }
        private bool ValidateConnectionId(string connectionId)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical,
                    new ArgumentNullException(nameof(connectionId), $"Missing {nameof(connectionId)}")));
                return false;
            }
            return true;
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
        private void SendToClient(SocketDataType type, ChatDataType chatType, byte[] data)
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

                byte[] dataSend = new byte[data.Length + 1];
                dataSend[0] = (byte)chatType;
                Buffer.BlockCopy(data, 0, dataSend, 1, data.Length);

                connection.AddWork(new TaskObject
                {
                    TaskType = type,
                    Data = dataSend,
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
        private void SendToClient(VClient client, SocketDataType type, ChatDataType chatType, byte[] data)
        {
            try
            {
                if (client == null)
                {
                    ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new InvalidOperationException("Does not exists connection")));
                    return;
                }

                byte[] dataSend = new byte[data.Length + 1];
                dataSend[0] = (byte)chatType;
                Buffer.BlockCopy(data, 0, dataSend, 1, data.Length);

                client.AddWork(new TaskObject
                {
                    TaskType = type,
                    Data = dataSend,
                    IsSendHeader = true,
                    SessionId = client.SocketId,
                    Priority = QueuePriority.High
                });
            }
            catch (Exception ex)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, ex));
                return;
            }
        }
        private void SendFileToClient(VClient client, SocketDataType type, ChatDataType chatType, ChunkFileInfo chunkInfo)
        {
            try
            {
                if (client == null)
                {
                    ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new InvalidOperationException("Does not exists connection")));
                    return;
                }

                byte[] dataSend = new byte[1];
                dataSend[0] = (byte)chatType;

                client.AddWork(new TaskObject
                {
                    TaskType = type,
                    Data = dataSend,
                    IsSendHeader = true,
                    SessionId = client.SocketId,
                    Priority = QueuePriority.High,
                    ChunkFileInfo = chunkInfo
                });
            }
            catch (Exception ex)
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
                    _chatAttachmentService.FileDataReceivedEvent -= FileDataReceivedEventHandler;
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
        private void FileDataReceivedEventHandler(object sender, FileEventArgs e)
        {
            ProgressBarUpdateEvent?.Invoke(this, new ChatControlProgressBarUpdateUIEventArgs(e.FileId, e.Size, e.Status));
        }
        private void P2PChatReceivedEventHandler(object sender, P2PChatEventArgs e)
        {
            try
            {
                if (sender is VClient client)
                {
                    ChatDataType type = e.Data[0] is byte b ? (ChatDataType)b : ChatDataType.None;
                    byte[] data = new byte[e.Data.Length - 1];
                    Buffer.BlockCopy(e.Data, 1, data, 0, data.Length);
                    switch (type)
                    {
                        case ChatDataType.Message:
                            ProcessMessage(client, data);
                            break;
                        case ChatDataType.RequestSendFile:
                            ProcessRequestSendFile(client, data);
                            break;
                        case ChatDataType.AcceptedSendFile:
                            ProcessAcceptSendFile(client, data);
                            break;
                        case ChatDataType.DeclinedSendFile:
                            ProcessDeclineSendFile(client, data);
                            break;
                        case ChatDataType.FileData:
                            ProcessFileDataReceived(client, data);
                            break;
                        default:
                            break;

                    }
                }
            }
            catch(Exception ex)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, ex));
            }
        }
        private void ProcessRequestSendFile(VClient client, byte[] data)
        {
            try
            {
                if (!_chatAttachmentService.ReceivedFileInfo(data, false, out VFileInfo info))
                {
                    ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new InvalidOperationException("Error when received request send file")));
                    return;
                }
                AddedEvent?.Invoke(this, new ChatControlAddedEventArgs(ChatControlType.ReceivedAttachment, client.SocketId, null, info));
            }
            catch(Exception ex)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, ex));
                return;
            }
        }
        private void ProcessMessage(VClient client, byte[] data)
        {
            try
            {
                string message = Helpers.ByteArrayHelper.ConvertByteArrayToString(data, Enums.EncodingType.UTF8).GetResult();
                SaveChatText(client.SocketId, ChatContentTypeEnum.Message, ChatOwnerEnum.Partner, message);
                AddedEvent?.Invoke(this, new ChatControlAddedEventArgs(ChatControlType.Message, client.SocketId, message, null));
            }
            catch(Exception ex)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, ex));
                return;
            }
        }
        private void ProcessAcceptSendFile(VClient client, byte[] data)
        {
            try
            {
                string fileId = Helpers.ByteArrayHelper.ConvertByteArrayToString(data, 0, 16, EncodingType.ASCII).GetResult();
                if (string.IsNullOrWhiteSpace(fileId))
                {
                    ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new InvalidOperationException("FileId is null or empty")));
                    return;
                }
                UpdateEvent?.Invoke(this, new ChatControlUpdateEventArgs(ChatControlType.AcceptAttachment, fileId));
                var chunks =  _chatAttachmentService.GetFileChunksInfo(fileId);
                for(int i = 0; i < chunks.Count; i++)
                {
                    SendFileToClient(client, SocketDataType.Chat, ChatDataType.FileData, chunks[i]);
                }
            }
            catch(Exception ex)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, ex));
                return;
            }
        }
        private void ProcessDeclineSendFile(VClient client, byte[] data)
        {
            try
            {
                string fileId = Helpers.ByteArrayHelper.ConvertByteArrayToString(data, 0, 16, EncodingType.ASCII).GetResult();
                if (string.IsNullOrWhiteSpace(fileId))
                {
                    ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new InvalidOperationException("FileId is null or empty")));
                    return;
                }
                UpdateEvent?.Invoke(this, new ChatControlUpdateEventArgs(ChatControlType.RejectAttachment, fileId));
            }
            catch (Exception ex)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, ex));
                return;
            }
        }
        private void ProcessFileDataReceived(VClient client, byte[] data)
        {
            try
            {
                _chatAttachmentService.ProcessFileDataReceived(data);
            }
            catch (Exception ex)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, ex));
            }
        }
        #endregion
    }
}
