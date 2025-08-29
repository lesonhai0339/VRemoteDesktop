using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Layouts;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.File;
using VRemoteDesktop.Services.VTCPClient;
using static VRemoteDesktop.Utils.DefaultValue;

namespace VRemoteDesktop.ViewModels
{
    public class ChatViewModel:IDisposable
    {
        private readonly object _lock = new object();
        private readonly int CHUNK_SIZE = DEFAULT_CHUNK_SIZE;
        private readonly IChatManager<VClient, object> _chatConnections;
        private string _currentConnectionActivate;
        private FileReceivedLayout _curFileReceived;
        private readonly IVFileExtension _fileExtension;

        public event EventHandler<ChatControlAddedEventArgs> AddeddEvent;
        public event EventHandler<ChatControlRemoveEventArgs> RemovedEvent;
        public event EventHandler<ChatControlUpdateEventArgs> UpdateEvent;
        public event EventHandler<ChatControlProgressBarUpdateEventArgs> ProgressBarEvent;
        public ChatViewModel()
        {
            _fileExtension = new VFileExtension();
            _fileExtension.FileEvent += FileEventHandler;
            _chatConnections = new ChatManager<VClient, object>();
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
                    string[] data = Helpers.StringHelper.StringToStringArrayWithSeparator(Encoding.UTF8.GetString(e.Data), "|");
                    var fileInfo = new VFileInfo
                    {
                        FileExtension = data[0],
                        Filename = data[1],
                        FileSize = long.Parse(data[2])
                    };
                    _fileExtension.Add(fileInfo, false);
                    
                    _curFileReceived = new FileReceivedLayout();
                    _curFileReceived.AcceptSaveFile += FileReceivedClickEventHandler;
                    _curFileReceived.Add(fileInfo);
                    AddeddEvent?.Invoke(this, new ChatControlAddedEventArgs(ChatControlType.Message, _curFileReceived));
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
                    AddeddEvent?.Invoke(this, new ChatControlAddedEventArgs(ChatControlType.Message, lb));
                }
                else if (e.Type == DataType.AcceptSendFile)
                {
                    SendFileRespondType respondType = (SendFileRespondType)e.Data[0];
                    if (respondType == SendFileRespondType.Accept)
                    {
                        UpdateEvent?.Invoke(this, new ChatControlUpdateEventArgs(ChatControlType.Message, ()=> _curFileReceived.UpdateRequestSendFileStatus("Đối tác đã chấp nhận")));
                        BeginSendFile(client);
                    }
                    else if(respondType == SendFileRespondType.Reject)
                    {
                        UpdateEvent?.Invoke(this, new ChatControlUpdateEventArgs(ChatControlType.Message, () => _curFileReceived.UpdateRequestSendFileStatus("Đối tác đã từ chối")));
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
                        int offset = BitConverter.ToInt32(e.Data, 0);
                        byte[] data = new byte[e.Data.Length - 4];
                        Buffer.BlockCopy(e.Data, 4, data, 0, data.Length);
                        ProgressBarEvent?.Invoke(this, new ChatControlProgressBarUpdateEventArgs(_curFileReceived, data.Length));

                        _fileExtension.WriteDataToFile(offset, data);
                        //Helpers.FileHelper.WriteToFile(_fileStream, offset, data);           
                    }
                    catch(Exception ex)
                    {
                        throw;
                    }

                }
            }
        }
        private void BeginSendFile(VClient client)
        {
            try
            {
                FileInfo fileInfo = Helpers.FileHelper.GetFileInfo(_fileExtension.FilePath);
                long chunkNumber = Helpers.FileHelper.CalculateChunkNumber(fileInfo.Length, CHUNK_SIZE);
                int count = 0;
                while (count < chunkNumber)
                {
                    int offset = count * CHUNK_SIZE;
                    byte[] chunkData = Helpers.FileHelper.GetFileDataByOffset(fileInfo.FullName, offset, CHUNK_SIZE);

                    byte[] dataSend = new byte[chunkData.Length + 4]; //4 byte for offset
                    Buffer.BlockCopy(BitConverter.GetBytes(offset), 0, dataSend, 0, 4);
                    Buffer.BlockCopy(chunkData, 0, dataSend, 4, chunkData.Length);

                    client.AddWork(
                        new TaskObject
                        {
                            TaskType = DataType.FileTransfer,
                            Data = dataSend,
                            SessionId = client.SocketId,
                            IsSendHeader = true,
                            Priority = QueuePriority.Low
                        });
                    count++;
                    Console.WriteLine("Total file send: " + count);
                }
            }
            catch(Exception ex)
            {
                throw;
            }
            finally
            {
                _fileExtension.Clear();
            }
        }
        public void FileReceivedClickEventHandler(object sender, P2PFileReceivedEventArgs e)
        {
            if(sender is Button btn && btn.Parent is FileReceivedLayout parent)
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
                AddeddEvent?.Invoke(this, new ChatControlAddedEventArgs(ChatControlType.Connection, lbChat));
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
            AddeddEvent?.Invoke(this, new ChatControlAddedEventArgs(ChatControlType.Message, lb));
        }
        public void RequestSendFile()
        {
            using (var dialog = new OpenFileDialog())
            {
                DialogResult result = dialog.ShowDialog();
                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    string selectedPath = dialog.FileName;
                    try
                    {
                        FileInfo fileInfo = Helpers.FileHelper.GetFileInfo(selectedPath);
                        if (fileInfo != null)
                        {
                            VFileInfo vfileInfo = new VFileInfo
                            {
                                FileExtension = fileInfo.Extension,
                                Filename = fileInfo.Name,
                                FileSize = fileInfo.Length
                            };
                            _fileExtension.Add(vfileInfo, true);
                            _fileExtension.UpdateSavePath(selectedPath);

                            FileReceivedLayout file = new FileReceivedLayout();
                            file.Add(vfileInfo, true);
                            _curFileReceived = file;

                            string data = Helpers.StringHelper.StringBuilderWithSeparator("|", fileInfo.Extension, fileInfo.Name, fileInfo.Length);
                            byte[] byteArray = Helpers.ByteArrayHelper.ConvertStringToByteArray(data, Enums.EncodingType.UTF8).GetResult();
                            SendToClient(DataType.RequestSendFile, byteArray);

                            AddeddEvent?.Invoke(this, new ChatControlAddedEventArgs(ChatControlType.Message, file));
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw ex;
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }
                }
                else
                {
                    throw new InvalidOperationException("Open file dialog error");
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
            }
        }
    }
}
