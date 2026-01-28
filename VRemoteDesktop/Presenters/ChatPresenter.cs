using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Odbc;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.FileService;
using VRemoteDesktop.Services.FileService.DTOs;
using VRemoteDesktop.Services.FileService.Enums;
using VRemoteDesktop.Services.RemoteDesktop;
using VRemoteDesktop.Services.SessionManagement.Events.ClientSession;
using VRemoteDesktop.Utils;

namespace VRemoteDesktop.Presenters
{
    public class ChatPresenter : IDisposable
    {
        private readonly object _lock = new object();
        private const int LOAD_LIMIT = 5;
        private const int FILE_ID_LENGTH = 16;   
        private int _disposed = 0;

        private string _clientSessionActive;

        private readonly RemoteService _remoteService;
        private readonly IChatManager<object> _chatManager;
        private readonly ISaveChat _saveChat;
        private readonly IVChatAttachmentService _chatAttachment;

        public event EventHandler<ChatControlAddedEventArgs> AddedEvent;
        public event EventHandler<ChatControlRemoveEventArgs> RemovedEvent;
        public event EventHandler<ChatControlUpdateEventArgs> UpdateEvent;
        public event EventHandler<ChatControlProgressBarUpdateUIEventArgs> ProgressBarUpdateEvent;
        public event EventHandler<ChatUpdateChatHistoryEventArgs> UpdateChatHistoryEvent;
        public event EventHandler<ChatErrorEventArgs> ErrorEvent;

        public ChatPresenter(RemoteService remoteService)
        {
            _remoteService = remoteService;

            _clientSessionActive = string.Empty;
            _saveChat = new VSaveChat();

            _chatAttachment = new VChatAttachmentService();
            _chatAttachment.FileDataReceivedEvent += FileDataReceivedEventHandler;

            _chatManager = new ChatManager<object>();
            _chatManager.ChatDisconnected += ChatDisconnectedEventHandler;

        }
        #region Properties
        public string ClientSessionActive
        {
            get
            {
                lock (_lock)
                {
                    return _clientSessionActive;
                }
            }
        }  
        #endregion
        #region Methods
        public void AddToChat(string sessionId, ClientSession clientSession)
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentNullException("Session Id cannot be null or empty");
            if(clientSession == null)
                throw new ArgumentNullException("Client session cannot be null");

            if (!_chatManager.Add(sessionId, clientSession))
                throw new Exception(string.Format("Cannot add client session with session id {0} in to chat", sessionId));

            //Register event to send direct chat data from client session to chat form instead send to service
            clientSession.OnChatReceived += ChatReceivedEventHandler;

            if(!AddToChat(sessionId))
            {
                _chatManager.Remove(sessionId);
                throw new Exception(string.Format("Cannot add session with id {0} into chat", sessionId));
            }
            SetActiveClientSession(sessionId);
        }
        public void RemoveChat(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentNullException("Session id cannot be null or empty");

            var clientSession = _chatManager.GetClientById(sessionId);
            if (clientSession == null)
                throw new InvalidOperationException(string.Format("ClientSession with id {0} not found", sessionId));

            //Un-Register event
            clientSession.OnChatReceived -= ChatReceivedEventHandler;

            if (!_chatManager.Remove(sessionId))
                throw new Exception(string.Format("Cannot remove chat with session id {0}", sessionId));

            var lastSession = _chatManager.GetLastConnectionId();
            SetActiveClientSession(lastSession);

            if(RemovedEvent != null)
                RemovedEvent.Invoke(this, new ChatControlRemoveEventArgs(sessionId, ChatControlType.Connection, sessionId));
        }
        public string GetNameBySessionId(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) 
                throw new ArgumentNullException("Session id cannot be null or empty");

            var session = _chatManager.GetClientById(sessionId);
            if (session == null)
                throw new InvalidOperationException(string.Format("Session with id {0} not found", sessionId));

            if(session.PartnerInfo != null && !string.IsNullOrEmpty(session.PartnerInfo.ComputerName))
            {
                return session.PartnerInfo.ComputerName;
            }
            else
            {
                return "Không xác định";
            }
        }
        public bool ContainsSessionChat(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentNullException("Session id cannot be null or empty");

            return _chatManager.ContainsKey(sessionId);
        }
        public bool SetFileSavePath(string fileId, string filePath)
        {
            if (string.IsNullOrEmpty(fileId))
                throw new ArgumentNullException("File id cannot be null or empty");
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException("File path cannot be null or empty");
            try
            {
                _chatAttachment.UpdateFileSavePath(fileId, filePath);   
                return true;
            }
            catch
            {
                return false;
            }
        }
        public bool SaveConversation(string sessionId, string filePath, string fileName, long fileSize) 
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentNullException("sessionId cannot be null or empty");
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException("filePath cannot be null or empty");
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentNullException("fileName cannot be null or empty");
            if (fileSize <= 0)
                throw new ArgumentOutOfRangeException("File size cannot less than or equal zero");

            return LogToFile(sessionId, ChatContentTypeEnum.File, ChatOwnerEnum.Partner, null, filePath, fileName, fileSize);
        }
        public bool AcceptFile(string fileId)
        {
            if (string.IsNullOrEmpty(fileId))
                throw new ArgumentNullException("File id cannot be null or empty");
            try
            {
                if (string.IsNullOrEmpty(_clientSessionActive))
                    return false;

                var session = _chatManager.GetClientById(_clientSessionActive);
                if (session == null)
                    return false;

                AddWorkToClient(
                    clientSession: session, 
                    type: SocketDataType.ChatSend, 
                    chatType: ChatDataType.AcceptSendFile, 
                    data: Encoding.ASCII.GetBytes(fileId));

                return true;
            }
            catch
            {
                return false;
            }
        }
        public bool RejectFile(string fileId)
        {
            if (string.IsNullOrEmpty(fileId))
                throw new ArgumentNullException("File id cannot be null or empty");
            try
            {
                if (string.IsNullOrEmpty(_clientSessionActive))
                    return false;

                var session = _chatManager.GetClientById(_clientSessionActive);
                if (session == null)
                    return false;

                AddWorkToClient(
                    clientSession: session,
                    type: SocketDataType.ChatSend,
                    chatType: ChatDataType.RejectSendFile,
                    data: Encoding.ASCII.GetBytes(fileId));

                return true;
            }
            catch
            {
                return false;
            }
        }
        public bool CancelFileTransfer(string fileId)
        {
            if (string.IsNullOrEmpty(fileId))
                throw new ArgumentNullException("File id cannot be null or empty");

            try
            {
                if (string.IsNullOrEmpty(fileId))
                    throw new ArgumentNullException("File id cannot be null or empty");
                try
                {
                    if (string.IsNullOrEmpty(_clientSessionActive))
                        return false;

                    var session = _chatManager.GetClientById(_clientSessionActive);
                    if (session == null)
                        return false;

                    AddWorkToClient(
                        clientSession: session,
                        type: SocketDataType.ChatSend,
                        chatType: ChatDataType.CancelFileData,
                        data: Encoding.ASCII.GetBytes(fileId));

                    return true;
                }
                catch
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }
        public bool GetActiveChatId(out string activeId)
        {
            activeId = string.Empty;
            lock (_lock)
            {
                if(!string.IsNullOrEmpty(_clientSessionActive))
                {
                    activeId = _clientSessionActive;
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
        public void SendMessage(string msg)
        {
            if(string.IsNullOrWhiteSpace(msg))
                return;
            try
            {
                if(GetActiveChatId(out string id))
                {
                    var session = _chatManager.GetClientById(id);
                    if (session == null)
                        return;

                    AddWorkToClient(
                        clientSession: session,
                        type: SocketDataType.ChatSend,
                        chatType: ChatDataType.Message,
                        data: Encoding.UTF8.GetBytes(msg)
                    );

                    if (LogToFile(id, ChatContentTypeEnum.Message, ChatOwnerEnum.Me, msg))
                    {
                        AddedEvent?.Invoke(this, new ChatControlAddedEventArgs(ChatControlType.Message, id, msg, null));
                    }
                }
            }
            catch
            {
                throw;
            }
        }
        public bool RequestSendFile()
        {
            try
            {
                var fileInfo = _chatAttachment.GetFileSendInfo();
                if (fileInfo == null)
                    return false;

                string info = StringHelper.StringBuilderWithSeparator(
                   separator: _remoteService.Separator,
                   fileInfo.Id,
                   fileInfo.Filename,
                   fileInfo.FileExtension,
                   fileInfo.FileSize,
                   fileInfo.Checksum
                );

                if(GetActiveChatId(out string id))
                {
                    var session = _chatManager.GetClientById(id);
                    if (session == null)
                        return false;

                    AddWorkToClient(session, SocketDataType.ChatSend, ChatDataType.RequestSendFile, Encoding.ASCII.GetBytes(info));

                    if(LogToFile(id, ChatContentTypeEnum.File, ChatOwnerEnum.Me, null, fileInfo.FilePath, fileInfo.Filename, fileInfo.FileSize))
                    {
                        if (AddedEvent != null)
                            AddedEvent.Invoke(this, new ChatControlAddedEventArgs(ChatControlType.RequestAttachment, id, null, fileInfo));

                        return true;
                    }
                }
                return false;
            }
            catch
            {
                throw;
            }
        }
        public bool LoadConversationHistory(string sessionId)
        {
            try
            {
                if (string.IsNullOrEmpty(sessionId))
                    return false;

                var conversationPath = GetConversationPath(sessionId);
                if (string.IsNullOrEmpty(conversationPath))
                    return false;

                object[] messages = _saveChat.ReadLastMessagesObject(conversationPath, LOAD_LIMIT);
                if (messages.Length <= 0)
                    return false;

                if (UpdateChatHistoryEvent != null)
                    UpdateChatHistoryEvent.Invoke(this, new ChatUpdateChatHistoryEventArgs(ChatUpdateChatHistoryEventType.LoadHistory, sessionId, messages));

                return true;
            }
            catch
            {
                throw;
            }
        }


        private bool LogToFile(string sessionId, ChatContentTypeEnum type, ChatOwnerEnum owner, string message = null, string filePath = null, string fileName = null, long fileSize = 0)
        {
            string path = GetConversationPath(sessionId);
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            switch (type)
            {
                case ChatContentTypeEnum.File:
                    if (string.IsNullOrWhiteSpace(filePath) && string.IsNullOrWhiteSpace(fileName) && fileSize <= 0)
                    {
                        return false;
                    }
                    _saveChat.Add(new ChatMessage(savePath: path, new ChatFile(type, owner, filePath, fileName, fileSize, DateTime.Now)));
                    break;
                case ChatContentTypeEnum.Message:
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        return false;
                    }
                    _saveChat.Add(new ChatMessage(savePath: path, new ChatText(type, owner, message, DateTime.Now)));
                    break;
                default:
                    break;
            }
            return true;
        }
        private string GetConversationPath(string connectionId)
        {
            try
            {
                var connection = _chatManager.GetClientById(connectionId);
                if (connection == null)
                {
                    return string.Empty;
                }
                string savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultChat.DEFAULT_CHAT_FOLDER, connection.PartnerInfo.ComputerName + ".txt");
                return savePath;
            }
            catch (Exception ex)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Warning, ex, "Tải lịch sử hội thoại"));
                return string.Empty;
            }
        }
        public void SetActiveClientSession(string sessionId)
        {
            lock (_lock)
            {
                _clientSessionActive = sessionId;
            }
        }
        private bool AddToChat(string sessionId)
        {
            var clientSession = _chatManager.GetClientById(sessionId);
            if (clientSession == null)
            {
                return false;
            }
            AddedEvent?.Invoke(this, new ChatControlAddedEventArgs(ChatControlType.Connection, clientSession.SessionId, clientSession.PartnerInfo.ComputerName, null));
            return true;
        }
        private void AddWorkToClient(ClientSession clientSession, SocketDataType type, ChatDataType chatType, byte[] data = null, ChunkFileInfo chunk = null)
        {
            try
            {
                byte[] payload = data != null
                       ? new byte[data.Length + 1]
                       : new byte[1];

                payload[0] = (byte)chatType;
                if (data != null) Buffer.BlockCopy(data, 0, payload, 1, data.Length);

                QueuePriority priority = (chatType == ChatDataType.FileData)
                    ? QueuePriority.Low
                    : QueuePriority.High;

                clientSession.AddWork(
                    priority,
                    new TaskObject
                    {
                        TaskType = type,
                        Data = payload,
                        IsSendHeader = true,
                        SessionId = clientSession.SessionId,
                        ChunkFileInfo = chunk
                    });
            }
            catch (Exception ex)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, ex, "Gửi dữ liệu"));
                return;
            }
        }

        #endregion

        #region Events
        private void ChatDisconnectedEventHandler(object sender, ChatDisconnectedEventArgs e)
        {
            RemoveChat(e.SessionId);
            if (RemovedEvent != null)
            {
                RemovedEvent.Invoke(this, new ChatControlRemoveEventArgs(e.SessionId, ChatControlType.Connection, e.SessionId));
            }
        }
        private void FileDataReceivedEventHandler(object sender, FileEventArgs e)
        {
            if (e.Status == FileStatus.CheckSumFailed)
            {
                _chatAttachment.CleanUpFileInfo(e.FileId);
                return;
            }
            ProgressBarUpdateEvent?.Invoke(this, new ChatControlProgressBarUpdateUIEventArgs(e.ConnectionId, e.FileId, e.Size, e.Status));
        }
        private void ChatReceivedEventHandler(object sender, ClientSessionDataReceivedEventArgs e)
        {
            try
            {
                if (sender is ClientSession clientSession)
                {
                    //First byte always ChatDataType, see more at Send(..) method above
                    ChatDataType type = e.Data[0] is byte b ? (ChatDataType)b : ChatDataType.None;
                    byte[] data = new byte[e.Data.Length - 1];
                    Buffer.BlockCopy(e.Data, 1, data, 0, data.Length);

                    switch (type)
                    {
                        case ChatDataType.Message:
                            MessageEventHandler(clientSession, data);
                            break;
                        case ChatDataType.RequestSendFile:
                            RequestSendFileEventHandler(clientSession, data);
                            break;
                        case ChatDataType.AcceptSendFile:
                            AcceptSendFileEventHandler(clientSession, data);
                            break;
                        case ChatDataType.RejectSendFile:
                            RejectSendFileEventHandler(clientSession, data);
                            break;
                        case ChatDataType.FileData:
                            FileDataEventHandler(clientSession, data);
                            break;
                        case ChatDataType.CancelFileData:
                            CancelFileDataEventHandler(clientSession, data);
                            break;
                        default:
                            Logger.Log.ForContext("", "ChatPresenter").Warning(string.Format("ChatReceivedEventHandler err: invalid type {0}", type));
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                if(ErrorEvent != null)
                    ErrorEvent.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Warning, ex, "Xử lý dữ liệu"));
            }
        }
        private void MessageEventHandler(ClientSession clientSession, byte[] data)
        {
           string message = Encoding.UTF8.GetString(data);
           if(LogToFile(clientSession.SessionId, ChatContentTypeEnum.Message, ChatOwnerEnum.Partner, message))
           {
              if(AddedEvent != null)
                AddedEvent.Invoke(this, new ChatControlAddedEventArgs(ChatControlType.Message, clientSession.SessionId, message, null, clientSession.PartnerInfo.ComputerName));
           }
        }
        private void RequestSendFileEventHandler(ClientSession clientSession, byte[] data)
        {
            if (!_chatAttachment.ReceivedFileInfo(data, false, out VFileInfo info))
            {
                if(ErrorEvent != null)
                    ErrorEvent.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Warning, new InvalidOperationException("Error when received request send file"), "Gửi file"));
                return;

            }
            if (AddedEvent != null)
                AddedEvent.Invoke(this, new ChatControlAddedEventArgs(ChatControlType.ReceivedAttachment, clientSession.SessionId, null, info));
        }
        private void AcceptSendFileEventHandler(ClientSession clientSession, byte[] data)
        {
            string fileId = Encoding.ASCII.GetString(data);
            if (string.IsNullOrWhiteSpace(fileId))
            {
                if(ErrorEvent != null)
                    ErrorEvent.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Warning, new InvalidOperationException("FileId is null or empty"), "Chấp nhận file"));
                return;
            }
            
            if(UpdateEvent != null)
                UpdateEvent.Invoke(this, new ChatControlUpdateEventArgs(clientSession.SessionId, ChatControlType.AcceptAttachment, fileId));

            //Calculate number of chunks need to send, offset and size each chunk
            List<ChunkFileInfo> chunks = _chatAttachment.CalculateNumberOfChunksFromFileByFileId(fileId);
            if (chunks.Count == 0)
            {
                if (ErrorEvent != null)
                    ErrorEvent.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Warning, new InvalidOperationException(string.Format("Cannot calculate chunks file from file with id {0}", fileId)), "Chấp nhận file"));
                return;
            }
            for (int i = 0; i < chunks.Count; i++)
            {
                AddWorkToClient(clientSession, SocketDataType.ChatSend, ChatDataType.FileData, null, chunks[i]);

            }

            //remove file info after add file chunks to queue
            _chatAttachment.RemoveFileInfo(fileId);
        }
        private void RejectSendFileEventHandler(ClientSession clientSession, byte[] data)
        {
            string fileId = Encoding.ASCII.GetString(data);
            if (string.IsNullOrWhiteSpace(fileId))
            {
                if(ErrorEvent != null)
                    ErrorEvent.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Warning, new InvalidOperationException("FileId is null or empty"), "Từ chối file"));
                return;
            }
            if(UpdateEvent != null)
                UpdateEvent.Invoke(this, new ChatControlUpdateEventArgs(clientSession.SessionId, ChatControlType.RefuseAttachment, fileId));
        }
        private void FileDataEventHandler(ClientSession clientSession, byte[] data)
        {
            _chatAttachment.ProcessFileDataReceived(clientSession.SessionId, data);
        }
        private void CancelFileDataEventHandler(ClientSession clientSession, byte[] data)
        {
            string fileId = Encoding.ASCII.GetString(data, 0, FILE_ID_LENGTH);
            if (string.IsNullOrEmpty(fileId))
            {
                Logger.Log.ForContext("", "ChatPresenter").Error("Cancel send file err: cannot get file id");
                return;
            }

            if(UpdateEvent != null)
                UpdateEvent.Invoke(this, new ChatControlUpdateEventArgs(clientSession.SessionId, ChatControlType.StopSendingAttachment, fileId));

            //Need to find which VClient sending this file but now using for to send stop send file with specific file id to all Vclient
            var connections = _chatManager.GetAllConnection();
            foreach (var connection in connections)
            {
                connection.RemoveFile(fileId);
            }
        }
        #endregion

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;

            try
            {
                if (disposing)
                {
                    if (_saveChat != null)
                        _saveChat.Dispose();
                    if(_chatAttachment != null)
                    {
                        _chatAttachment.FileDataReceivedEvent -= FileDataReceivedEventHandler;
                        _chatAttachment.Dispose();
                    }
                    if(_chatManager != null)
                    {
                        _chatManager.ChatDisconnected += ChatDisconnectedEventHandler;
                        _chatManager.Dispose();
                    }
                }
            }
            catch { }
        }
    }
}
