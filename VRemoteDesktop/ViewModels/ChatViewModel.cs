using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.Events;
using VRemoteDesktop.Layouts;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.File;
using VRemoteDesktop.Services.VTCPClient;
using static VRemoteDesktop.Utils.DefaultValue;

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
        private readonly object _lock = new object();
        private readonly int CHUNK_SIZE = DEFAULT_CHUNK_SIZE;
        private readonly IChatManager<VClient, object> _chatConnections;
        private string _clientAdded;
        private string _clientRemoved;
        private string _currentConnectionActivate;
        private FileReceivedLayout _curFileReceived;
        private readonly IVFileExtension _fileExtension;
        public event Action<Control> ControlEvent;
        public event Action<Control, int> TestEvent;
        public ChatViewModel()
        {
            _fileExtension = new VFileExtension();
            _fileExtension.FileEvent += FileEventHandler;
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
                    _fileExtension.Add(new VFileInfo
                    {
                        FileExtension = data[0],
                        Filename = data[1],
                        FileSize = long.Parse(data[2])
                    }, false);
                    
                    _curFileReceived = new FileReceivedLayout();
                    _curFileReceived.Add(new VFileInfo
                    {
                        FileExtension = data[0],
                        Filename = data[1],
                        FileSize = long.Parse(data[2])
                    });
                    _curFileReceived.ClickedEvent += FileReceivedClickEventHandler;
                    ControlEvent?.Invoke(_curFileReceived);
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
                        _curFileReceived.PartnerAcceptSendFile();
                        BeginSendFile(client);
                    }
                    else
                    {
                        _curFileReceived.PartnerRejectSendFile();
                        MessageBox.Show("Partner Rejected send file");
                    }
                }
                else if (e.Type == DataType.FileTransfer)
                {
                    try
                    {
                        int offset = BitConverter.ToInt32(e.Data, 0);
                        byte[] data = new byte[e.Data.Length - 4];
                        Buffer.BlockCopy(e.Data, 4, data, 0, data.Length);
                        _fileExtension.WriteDataToFile(offset, data);
                        //Helpers.FileHelper.WriteToFile(_fileStream, offset, data);
                        TestEvent?.Invoke(_curFileReceived, data.Length);
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
                            Priority = TaskObjectPriority.Low
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
        private void FileReceivedClickEventHandler(object sender, P2PFileReceivedEventArgs e)
        {
            if(sender is Button btn && btn.Parent is FileReceivedLayout pr)
            {
                //Accept file
                if (string.Compare(btn.Name, "btnSave") == 0)
                {
                    _fileExtension.UpdateSavePath(e.filePath);
                    //_savePath = e.filePath;
                    //InitializeFileTransfer(_savePath);
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
                        pr.RemoveButton();
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
                            Data = new byte[] { 0 },
                            IsSendHeader = true,
                            SessionId = client.SocketId
                        });
                        pr.RemoveButton();
                    }
                }
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public Label NewControl(string id, int width)
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
                    Width = width - 2,
                    Height = 20,
                    Margin = Padding.Empty
                };
                lbChat.Click += ChangeConnectionActivate;
                lbChat.MouseHover += (s, e) =>
                {
                    lbChat.Cursor = Cursors.Hand;
                };
                lbChat.MouseLeave += (s, e) =>
                {
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
            ControlEvent?.Invoke(lb);
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
