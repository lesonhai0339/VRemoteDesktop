using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.FileService;
using VRemoteDesktop.Services.VTCPClient;
using VRemoteDesktop.Utils;
using static System.Net.WebRequestMethods;
namespace VRemoteDesktop.ViewModels
{
    public class ChatViewModel: IDisposable
    {
        private bool _disposed = false;
        private readonly object _lock = new object();
        private string _currentConnectionActivate;
        private readonly Dictionary<ChatDataType, Action<VClient, byte[]>> _handlers;
        private readonly IChatManager<object> _chatConnections;

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
            _currentConnectionActivate = string.Empty;
            _handlers = new Dictionary<ChatDataType, Action<VClient, byte[]>>()
            {
                { ChatDataType.Message, ProcessMessage },
                { ChatDataType.RequestSendFile, ProcessRequestSendFile },
                { ChatDataType.AcceptedSendFile, ProcessAcceptSendFile },
                { ChatDataType.DeclinedSendFile, ProcessDeclineSendFile },
                { ChatDataType.FileData, ProcessFileDataReceived },
                { ChatDataType.StopReceivedFileData, ProcessPartnerStopReceiveFile },
            };
            _saveChat = new SaveChat(); 
            _chatAttachmentService = new VChatAttachmentService();
            _chatAttachmentService.FileDataReceivedEvent += FileDataReceivedEventHandler;

            _chatConnections = new ChatManager<object>();
            _chatConnections.ChatDisconnected += ChatDisconnectedEventHandler;
        }
        #region Properties
        #endregion
        #region Methods
        /// <summary>
        /// Add reference to VClient(contain socket connection to partner) to chat manager after connect succeed
        /// </summary>
        /// <param name="connectionId">SocketId</param>
        /// <param name="connection">Reference to VClient</param>
        /// <returns><see cref="ChatRespond{T}"/><see cref="bool"/></returns>
        public ChatRespond<bool> AddConnection(string connectionId, VClient connection)
        {
            if (!StringValidate<bool>(connectionId, nameof(connectionId), out var respond))
            {
                return respond;
            }

            if (connection == null)
                return ChatRespondHelper.Failed<bool>(
                    message: string.Format("Thiếu tham số {0}", nameof(connection)),
                    systemMessage: string.Format("Missing arguments for {0}", nameof(connection)));

            if (!_chatConnections.Add(connectionId, connection))
            {
                return ChatRespondHelper.Failed<bool>(
                    systemMessage: string.Format("Cannot add connection with id {0} to chat", connectionId));
            }
            connection.P2PChatReceived += P2PChatReceivedEventHandler;
            bool flag =  AddChatConnection(connectionId);
            if(!flag)
            {
                return ChatRespondHelper.Failed<bool>(
                       systemMessage: string.Format("Add connection with id {0} to chat failed", connectionId));
            }
            SetCurrentConnectionActivate(connectionId);
            return ChatRespondHelper.Success<bool>(
                systemMessage: string.Format("Added connection with id {0} to chat", connectionId));
        }
        /// <summary>
        /// Remove reference of VClient out chat manager and unregister event
        /// </summary>
        /// <param name="connectionId">SocketId</param>
        /// <returns><see cref="ChatRespond{T}"/><see cref="bool"/></returns>
        public ChatRespond<bool> RemoveConnection(string connectionId)
        {
            if (!StringValidate<bool>(connectionId, nameof(connectionId), out var respond))
            {
                return respond;
            }

            var connection = _chatConnections.GetClientById(connectionId);
            if (connection == null)
            {
                return ChatRespondHelper.Failed<bool>(
                    message: string.Format("Xảy ra lỗi khi xóa chat connection"),
                    systemMessage: string.Format("Does not exists connection with id {0} in chat connections", connectionId));
            }
            connection.P2PChatReceived -= P2PChatReceivedEventHandler;
            bool flag = _chatConnections.Remove(connectionId);
            if (!flag)
            {
                return ChatRespondHelper.Failed<bool>(
                    message: string.Format("Xảy ra lỗi khi xóa chat connection"),
                    systemMessage: string.Format("Cannot remove connection with id {0} from chat connections", connectionId));
            }
            //possible empty
            string id = _chatConnections.GetLastConnectionId();
            SetCurrentConnectionActivate(id);
            RemovedEvent?.Invoke(this, new ChatControlRemoveEventArgs(ChatControlType.Connection, connectionId));

            return ChatRespondHelper.Success<bool>(
                    message: string.Format("Xóa chat connection thành công"),
                    systemMessage: string.Format("Removed connection with id {0}", connectionId));
        }
        //Call invoke to UI to create new control contain new chat connection info
        private bool AddChatConnection(string connectionId)
        {
            var connection = _chatConnections.GetClientById(connectionId);
            if(connection == null)
            {
                return false;
            }
            AddedEvent?.Invoke(this, new ChatControlAddedEventArgs(ChatControlType.Connection, connection.SocketId, connection.Partner.ComputerName, null));
            return true;    
        }
        /// <summary>
        /// Get partner computer name using connectionId(SocketId), to show partner name on chat panel
        /// </summary>
        /// <param name="connectionId"></param>
        /// <returns><see cref="ChatRespond{T}"/><see cref="string"/></returns>
        public ChatRespond<string> GetConnectionNameById(string connectionId)
        {
            if (!StringValidate<string>(connectionId, nameof(connectionId), out var respond))
            {
                return respond;
            }

            var connection = _chatConnections.GetClientById(connectionId);
            if (connection == null)
            {
                return ChatRespondHelper.Failed<string>(
                    message: string.Format("Không tìm thấy connection với id {0}", connectionId),
                    systemMessage: string.Format("Cannot find connection with id {0}", connectionId));
            }
            return ChatRespondHelper.Success<string>(
                    systemMessage: string.Format("Success {0}, {1}", connectionId, nameof(GetConnectionNameById)),
                    data: connection.Partner.ComputerName);
        }
        /// <summary>
        /// Check connection still exists in chat manager or not
        /// </summary>
        /// <param name="connectionId"></param>
        /// <returns><see cref="ChatRespond{T}"/><see cref="bool"/></returns>
        public ChatRespond<bool> IsValidConnection(string connectionId)
        {
            if (!StringValidate<bool>(connectionId, nameof(connectionId), out var respond))
            {
                return respond;
            }

            bool flag =  _chatConnections.ContainsKey(connectionId);
            if (!flag)
            {
                return ChatRespondHelper.Failed<bool>(
                    message: string.Format("Không tìm thấy connection với id {0}", connectionId),
                    systemMessage: string.Format("Cannot find connection with id {0}", connectionId));
            }
            return ChatRespondHelper.Success<bool>(
                   systemMessage: string.Format("IsValidConnection success for {0}", nameof(connectionId)),
                   data: true);
        }
        /// <summary>
        /// Sets the current active connection to the specified connection Id, activate when user click on new connection control or an chat connection removed 
        /// </summary>
        /// <param name="connectionId"></param>
        /// <returns><see cref="ChatRespond{T}"/><see cref="bool"/></returns>
        public ChatRespond<bool> SetCurrentConnectionActivate(string connectionId)
        {
            if (!StringValidate<bool>(connectionId, nameof(connectionId), out var respond))
            {
                return respond;
            }
            lock (_lock)
            {
                _currentConnectionActivate = connectionId;
            }
            return ChatRespondHelper.Success<bool>(
                    systemMessage: string.Format("Set current connection activate {0} success", nameof(connectionId)));
        }
        /// <summary>
        /// Set save file path to specified file by file id
        /// </summary>
        /// <param name="fileId"></param>
        /// <param name="filePath"></param>
        /// <returns><see cref="ChatRespond{T}"/><see cref="bool"/></returns>
        public ChatRespond<bool> UpdateFileSavePath(string fileId, string filePath)
        {
            if (!StringValidate<bool>(fileId, nameof(fileId), out var respond))
                return respond;

            if (!StringValidate<bool>(filePath, nameof(filePath), out var respond2))
                return respond2;

            _chatAttachmentService.UpdateFileSavePath(fileId, filePath);
            return ChatRespondHelper.Success<bool>(
                   systemMessage: string.Format("Update file path for {0} success", fileId));
        }
        /// <summary>
        /// Save chat sent and received to specified file by connection id(connection id will be use to file name, example: 112233.txt)
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="savePath"></param>
        /// <param name="fileName"></param>
        /// <param name="fileSize"></param>
        /// <returns><see cref="ChatRespond{T}"/><see cref="bool"/></returns>
        public ChatRespond<bool> SaveChatToFile(string connectionId, string savePath, string fileName, long fileSize)
        {
            if (!StringValidate<bool>(connectionId, nameof(connectionId), out var idRespond))
                return idRespond;

            if (!StringValidate<bool>(savePath, nameof(savePath), out var savePathRespond))
                return savePathRespond;

            if (!StringValidate<bool>(fileName, nameof(fileName), out var filenameRespond))
                return filenameRespond;

            if (fileSize <= 0)
            {
                return ChatRespondHelper.Failed<bool>(
                     message: string.Format("Sai định dạng dữ liệu {0}", fileSize),
                     systemMessage: string.Format("ArgumentOutOfRange {0} - {1}", typeof(long), fileSize));
            }
            SaveChat(connectionId, ChatContentTypeEnum.File, ChatOwnerEnum.Partner, null, savePath, fileName, fileSize);
            return ChatRespondHelper.Success<bool>(
                  systemMessage: string.Format("Save chat file on connection with id {0} success", connectionId));
        }
        /// <summary>
        /// Accepted "request send file" from partner. Send "accepted" packet to partner and partner will send file data after that
        /// </summary>
        /// <param name="fileId"></param>
        /// <returns><see cref="ChatRespond{T}"/><see cref="bool"/></returns>
        public ChatRespond<bool> AcceptedFile(string fileId)
        {
            try
            {
                if (!StringValidate<bool>(fileId, nameof(fileId), out var respond))
                    return respond;

                var connection = _chatConnections.GetClientById(_currentConnectionActivate);
                if (connection == null)
                {
                    return ChatRespondHelper.Failed<bool>(
                       message: string.Format("Xảy ra lỗi", nameof(AcceptedFile)),
                       systemMessage: string.Format("Cannot find connection on current id {0}", _currentConnectionActivate));
                }   

                byte[] data = Helpers.ByteArrayHelper.ConvertStringToByteArray(fileId, EncodingType.ASCII).GetResult();
                Send(connection, SocketDataType.Chat, ChatDataType.AcceptedSendFile, data);
                return ChatRespondHelper.Success<bool>(
                    systemMessage: string.Format("Accept send file on connection with id {0} success", _currentConnectionActivate),
                    data: true);
            }
            catch(Exception ex)
            {
                return ChatRespondHelper.Error<bool>(
                       message: string.Format("Xảy ra lỗi", nameof(AcceptedFile)),
                       systemMessage: string.Format("Unexcepted error on {0}, error: {1}", nameof(AcceptedFile), ex.Message));
            }
        }
        /// <summary>
        /// Declined "request send file" from partner. Send "declined" packet to partner and partner will send file data after that
        /// </summary>
        /// <param name="fileId"></param>
        /// <returns><see cref="ChatRespond{T}"/><see cref="bool"/></returns>
        public ChatRespond<bool> DeclinedFile(string fileId)
        {
            try
            {
                if (!StringValidate<bool>(fileId, nameof(fileId), out var respond))
                    return respond;

                var connection = _chatConnections.GetClientById(_currentConnectionActivate);
                if (connection == null)
                {
                    return ChatRespondHelper.Failed<bool>(
                     message: string.Format("Xảy ra lỗi", nameof(AcceptedFile)),
                     systemMessage: string.Format("Cannot find connection on current id {0}", _currentConnectionActivate));
                }
                byte[] data = ByteArrayHelper.ConvertStringToByteArray(fileId, EncodingType.ASCII).GetResult();
                Send(connection, SocketDataType.Chat, ChatDataType.DeclinedSendFile, data);  
                _chatAttachmentService.RemoveFileInfo(fileId);
                return ChatRespondHelper.Success<bool>(
                    systemMessage: string.Format("DeclinedFile send file on connection with id {0} success", _currentConnectionActivate),
                    data: true);
            }
            catch(Exception ex)
            {
                return ChatRespondHelper.Error<bool>(
                       message: string.Format("Xảy ra lỗi", nameof(DeclinedFile)),
                       systemMessage: string.Format("Unexcepted error on {0}, error: {1}", nameof(DeclinedFile), ex.Message));
            }
        }/// <summary>
         /// Stop received file data from specified file by fileId
         /// </summary>
         /// <param name="fileId"></param>
         /// <returns><see cref="ChatRespond{T}"/><see cref="bool"/></returns>
        public ChatRespond<bool> StopReceivedFileDataByFileId(string fileId)
        {
            try
            {
                if (!StringValidate<bool>(fileId, nameof(fileId), out var respond))
                    return respond;

                var connection = _chatConnections.GetClientById(_currentConnectionActivate);
                if (connection == null)
                {
                    return ChatRespondHelper.Failed<bool>(
                     message: string.Format("Xảy ra lỗi", nameof(AcceptedFile)),
                     systemMessage: string.Format("Cannot find connection on current id {0}", _currentConnectionActivate));
                }
                byte[] data = ByteArrayHelper.ConvertStringToByteArray(fileId, EncodingType.ASCII).GetResult();
                Send(connection, SocketDataType.Chat, ChatDataType.StopReceivedFileData, data);
                _chatAttachmentService.CleanUpFileInfo(fileId);
                return ChatRespondHelper.Success<bool>(
                    systemMessage: string.Format("DeclinedFile send file on connection with id {0} success", _currentConnectionActivate),
                    data: true);
            }
            catch (Exception ex)
            {
                return ChatRespondHelper.Error<bool>(
                       message: string.Format("Xảy ra lỗi", nameof(DeclinedFile)),
                       systemMessage: string.Format("Unexcepted error on {0}, error: {1}", nameof(DeclinedFile), ex.Message));
            }
        }
        /// <summary>
        /// Get current connection id
        /// </summary>
        /// <returns><see cref="ChatRespond{T}"/><see cref="string"/></returns>
        public ChatRespond<string> GetCurrentConnectionActivate()
        {
            if (!StringValidate<string>(_currentConnectionActivate, nameof(_currentConnectionActivate), out var respond))
            {
                return respond;
            }
            else
            {
                return ChatRespondHelper.Success<string>(systemMessage: "OK",data: _currentConnectionActivate);
            }
        }
        /// <summary>
        /// Send chat to partner on current connection
        /// </summary>
        /// <param name="chatData"></param>
        /// <returns><see cref="ChatRespond{T}"/><see cref="bool"/></returns>
        public ChatRespond<bool> SendChatMessage(string chatData)
        {
            try
            {
                Send(null, SocketDataType.Chat, ChatDataType.Message, Helpers.ByteArrayHelper.ConvertStringToByteArray(chatData, EncodingType.UTF8).GetResult());
                bool flag =  SaveChat(_currentConnectionActivate, ChatContentTypeEnum.Message, ChatOwnerEnum.Me, chatData);
                if (flag)
                {
                    AddedEvent?.Invoke(this, new ChatControlAddedEventArgs(ChatControlType.Message, _currentConnectionActivate, chatData, null));
                    return ChatRespondHelper.Success<bool>(
                        systemMessage: string.Format("Send message success on {0}", _currentConnectionActivate),
                        data: true);
                }
                return ChatRespondHelper.Failed<bool>(
                    message: string.Format("Gửi tin nhắn thất bại", nameof(SendChatMessage)),
                    systemMessage: string.Format("Send message failed on connection id {0}", _currentConnectionActivate));
            }
            catch(Exception ex)
            {
                return ChatRespondHelper.Error<bool>(
                   message: string.Format("Xảy ra lỗi khi gửi tin nhắn", nameof(SendChatMessage)),
                   systemMessage: string.Format("Error when send message on connection id {0}, ex: {1}", _currentConnectionActivate, ex.Message));
            }
        }
        /// <summary>
        /// Send request send file to partner on current connection
        /// </summary>
        /// <returns><see cref="ChatRespond{T}"/><see cref="bool"/></returns>
        public ChatRespond<bool> RequestSendFile()
        {
            try
            {
                var fileInfo = _chatAttachmentService.GetFileSendInfo();
                if(fileInfo == null)
                {
                    return ChatRespondHelper.Failed<bool>(
                        systemMessage: "Cannot get file info");
                }
                string data = Helpers.StringHelper.StringBuilderWithSeparator(DefaultValue.DEFAULT_SEPRATOR, fileInfo.Id, fileInfo.Filename, fileInfo.FileExtension, fileInfo.FileSize, fileInfo.Checksum);
                byte[] byteArray = Helpers.ByteArrayHelper.ConvertStringToByteArray(data, Enums.EncodingType.UTF8).GetResult();
                Send(null, SocketDataType.Chat, ChatDataType.RequestSendFile, byteArray);

                //Write to chat file
                SaveChat(_currentConnectionActivate, ChatContentTypeEnum.File, ChatOwnerEnum.Me, null, fileInfo.FilePath, fileInfo.Filename, fileInfo.FileSize);

                AddedEvent?.Invoke(this, new ChatControlAddedEventArgs(ChatControlType.RequestAttachment, _currentConnectionActivate, null, fileInfo));
                return ChatRespondHelper.Success<bool>(
                             systemMessage: string.Format("RequestSendFile success on connection id {0}", _currentConnectionActivate));
            }
            catch (Exception ex)
            {
                return ChatRespondHelper.Error<bool>(
                       message: string.Format("Xảy ra lỗi", nameof(RequestSendFile)),
                       systemMessage: string.Format("Request send file error on connection id {0}", _currentConnectionActivate));
            }
        }
        //Save chat to file
        private bool SaveChat(string id, ChatContentTypeEnum type, ChatOwnerEnum owner, string message = null, string filePath = null, string fileName = null, long fileSize = 0)
        {
            string savePath = GetChatPath(id);
            if (string.IsNullOrWhiteSpace(savePath))
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
                    _saveChat.Add(new ChatMessage(savePath: savePath,new ChatFile(type, owner, filePath, fileName, fileSize, DateTime.Now)));
                    break;
                case ChatContentTypeEnum.Message:
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        return false;
                    }
                    _saveChat.Add(new ChatMessage(savePath: savePath, new ChatText(type, owner, message, DateTime.Now)));
                    break;
                default:
                    break;
            }
            return true;
        }
        /// <summary>
        /// Load number of <see cref="DEFAULT_MESSAGE_LOAD"/> previous message on current connection 
        /// </summary>
        /// <param name="connectionId"></param>
        /// <returns><see cref="ChatRespond{T}"/><see cref="bool"/></returns>
        public ChatRespond<bool> LoadChatHistoryByConnectionId(string connectionId)
        {
            if (!StringValidate<bool>(connectionId, nameof(connectionId), out var respond))
            {
                return respond;
            }

            string chatHistoryFilePath = GetChatPath(connectionId);
            if (StringValidate<bool>(chatHistoryFilePath, nameof(chatHistoryFilePath), out var fileRespond))
            {
                object[] messages = _saveChat.ReadLastMessagesObject(chatHistoryFilePath, DefaultChat.DEFAULT_MESSAGE_LOAD);
                if (messages == null || messages.Length == 0)
                {
                    return ChatRespondHelper.Error<bool>(
                                 systemMessage: string.Format("Messages are empty on connection id {0}", connectionId));
                }
                UpdateChatHistoryEvent?.Invoke(this, new ChatUpdateChatHistoryEventArgs(ChatUpdateChatHistoryEventType.LoadHistory, connectionId, messages));

                return ChatRespondHelper.Success<bool>(systemMessage: string.Format("Load message success on connection {0}", connectionId));
            }

            return ChatRespondHelper.Failed<bool>(systemMessage: string.Format("Load messages failed on connection id {0}", connectionId));
        }
        //Check string
        private bool StringValidate<T>(string value, string nameOfValue, out ChatRespond<T> respond)
        {
            respond = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                respond = ChatRespondHelper.Failed<T>(
                   message: string.Format("Thiếu tham số {0}", nameOfValue),
                   systemMessage: string.Format("Missing arguments for {0}", nameOfValue));
                return false;
            }
            return true;
        }
        /// <summary>
        /// Get chat path by connection id on <see cref="DEFAULT_CHAT_FOLDER"/>
        /// </summary>
        /// <param name="connectionId"></param>
        /// <returns><see cref="string"/> File path</returns>
        private string GetChatPath(string connectionId)
        {
            try
            {
                var connection = _chatConnections.GetClientById(connectionId);
                if (connection == null)
                {
                    return string.Empty;
                }
                string savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultChat.DEFAULT_CHAT_FOLDER, connection.Partner.ComputerName + ".txt");
                return savePath;
            }
            catch(Exception ex)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, ex));
                return string.Empty;
            }
        }
        /// <summary>
        /// Add Chat Task to Sender Queue of <see cref="VClient"/> 
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="type"></param>
        /// <param name="chatType"></param>
        /// <param name="data"></param>
        /// <param name="chunk"></param>
        private void Send(VClient connection, SocketDataType type, ChatDataType chatType, byte[] data = null, ChunkFileInfo chunk = null)
        {
            try
            {
                if (connection == null)
                {
                    //using current connection
                    if (string.IsNullOrEmpty(_currentConnectionActivate))
                    {
                        SetCurrentConnectionActivate(_chatConnections.GetLastConnectionId());
                    }
                    connection = _chatConnections.GetClientById(_currentConnectionActivate);
                    if (connection == null)
                    {
                        ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new InvalidOperationException(string.Format("Does not exists connection with id {0}", _currentConnectionActivate))));
                        return;
                    }
                }

                byte[] payload = data != null
                       ? new byte[data.Length + 1]
                       : new byte[1];

                payload[0] = (byte)chatType;
                if (data != null) Buffer.BlockCopy(data, 0, payload, 1, data.Length);

                QueuePriority priority = (chatType == ChatDataType.FileData)
                    ? QueuePriority.Low
                    : QueuePriority.High;

                connection.AddWork(new TaskObject
                {
                    TaskType = type,
                    Data = payload,
                    IsSendHeader = true,
                    SessionId = connection.SocketId,
                    ChunkFileInfo = chunk
                }, priority);
            }
            catch (Exception ex)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, ex));
                return;
            }
        }
        #endregion
        #region Events
        //Event event handler from ChatManager, this event is used to remove chat connection UI when disconnected
        private void ChatDisconnectedEventHandler(object sender, ChatDisconnectedEventArgs e)
        {
            var result = RemoveConnection(e.SocketId);
            if (result.IsSuccess)
            {
                RemovedEvent?.Invoke(this, new ChatControlRemoveEventArgs(ChatControlType.Connection, e.SocketId));
            }
        }
        //Event event handler from FileService, this event is used to update progress bar UI when received file data, finally remove file out attachments when finished
        private void FileDataReceivedEventHandler(object sender, FileEventArgs e)
        {
            if(e.Status == FileStatus.CheckSumFailed)
            {
                _chatAttachmentService.CleanUpFileInfo(e.FileId);
                return;
            }
            ProgressBarUpdateEvent?.Invoke(this, new ChatControlProgressBarUpdateUIEventArgs(e.FileId, e.Size, e.Status));
        }
        //Chat data received from partner from VClient
        private void P2PChatReceivedEventHandler(object sender, P2PChatEventArgs e)
        {
            try
            {
                if (sender is VClient client)
                {
                    //First byte always ChatDataType, see more at Send(..) method above
                    ChatDataType type = e.Data[0] is byte b ? (ChatDataType)b : ChatDataType.None;
                    byte[] data = new byte[e.Data.Length - 1];
                    Buffer.BlockCopy(e.Data, 1, data, 0, data.Length);

                    if (_handlers.TryGetValue(type, out var handler))
                        handler(client, data);
                }
            }
            catch(Exception ex)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, ex));
            }
        }
        //Handler partner request send file
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
        //Handler partner send message
        private void ProcessMessage(VClient client, byte[] data)
        {
            try
            {
                string message = Helpers.ByteArrayHelper.ConvertByteArrayToString(data, Enums.EncodingType.UTF8).GetResult();
                bool flag = SaveChat(client.SocketId, ChatContentTypeEnum.Message, ChatOwnerEnum.Partner, message);
                if(flag)
                    AddedEvent?.Invoke(this, new ChatControlAddedEventArgs(ChatControlType.Message, client.SocketId, message, null, client.Partner.ComputerName));
            }
            catch(Exception ex)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, ex));
                return;
            }
        }
        //Handler partner accepted request send file
        private void ProcessAcceptSendFile(VClient client, byte[] data)
        {
            try
            {
                string fileId = Helpers.ByteArrayHelper.ConvertByteArrayToString(data, EncodingType.ASCII).GetResult();
                if (string.IsNullOrWhiteSpace(fileId))
                {
                    ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new InvalidOperationException("FileId is null or empty")));
                    return;
                }
                UpdateEvent?.Invoke(this, new ChatControlUpdateEventArgs(ChatControlType.AcceptAttachment, fileId));

                //Calculate number of chunks need to send, offset and size each chunk
                List<ChunkFileInfo> chunks =  _chatAttachmentService.CalculateNumberOfChunksFromFileByFileId(fileId);
                if(chunks.Count == 0)
                {
                    ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new InvalidOperationException(string.Format("Cannot calcutate chunks file from file with id {0}", fileId))));
                    return;
                }
                for (int i = 0; i< chunks.Count; i++)
                {
                    Send(client, SocketDataType.Chat, ChatDataType.FileData, null, chunks[i]);

                }

                //remove file info after add file chunks to queue
                _chatAttachmentService.RemoveFileInfo(fileId);
            }
            catch(Exception ex)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, ex));
                return;
            }
        }
        //Handler decline request send file from partner
        private void ProcessDeclineSendFile(VClient client, byte[] data)
        {
            try
            {
                string fileId = Helpers.ByteArrayHelper.ConvertByteArrayToString(data, EncodingType.ASCII).GetResult();
                if (string.IsNullOrWhiteSpace(fileId))
                {
                    ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, new InvalidOperationException("FileId is null or empty")));
                    return;
                }
                UpdateEvent?.Invoke(this, new ChatControlUpdateEventArgs(ChatControlType.RefuseAttachment, fileId));
            }
            catch (Exception ex)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, ex));
            }
        }
        //Handler file data received from partner
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
        private void ProcessPartnerStopReceiveFile(VClient client, byte[] arg2)
        {
            try
            {
                string fileId = Helpers.ByteArrayHelper.ConvertByteArrayToString(arg2, 0, RandomLength.FILE_ID_LENGTH, EncodingType.ASCII).GetResult();
                UpdateEvent?.Invoke(this, new ChatControlUpdateEventArgs(ChatControlType.StopSendingAttachment, fileId));

                //Need to find which VClient sending this file but now using for to send stop send file with specific file id to all Vclient
                var connections = _chatConnections.GetAllConnection();
                foreach (var connection in connections)
                {
                    connection.RemoveTaskByType(SocketDataType.Chat, ChatDataType.StopReceivedFileData, fileId);
                }
            }
            catch (Exception ex)
            {
                ErrorEvent?.Invoke(this, new ChatErrorEventArgs(ChatErrorLevel.Critical, ex));
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
            if (disposing)
            {
                if (_disposed) return;

                if (_chatAttachmentService != null)
                    _chatAttachmentService.FileDataReceivedEvent -= FileDataReceivedEventHandler;

                if (_chatConnections != null)
                    _chatConnections.ChatDisconnected -= ChatDisconnectedEventHandler;

                _chatAttachmentService.Dispose();
                _chatConnections.Dispose();
                _saveChat.Dispose();

                _handlers.Clear();
                _disposed = true;
            }
        }
    }
}
