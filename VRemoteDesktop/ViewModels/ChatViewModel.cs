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
        private readonly Dictionary<string, ChatConnection> _clients;
        private string _clientAdded;
        private string _clientRemoved;
        private string _currentConnectionActivate;
        public event Action<Control> ControlEvent;
        public ChatViewModel()
        {
            _clients = new Dictionary<string, ChatConnection>();
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
        public void UpdateConnection(string key, VClient value)
        {
            _clients.Add(key, new ChatConnection(value , new List<object>()));
            value.P2PChatReceived += P2PChatReceivedEventHandler;
            ClientAdded = key;
            _currentConnectionActivate = key;
        }
        public void RemoveConnection(string key)
        {
            if(_clients.TryGetValue(key, out var client))
            {
                client.Client.P2PChatReceived -= P2PChatReceivedEventHandler;
                _clients.Remove(key);
                ClientRemoved = key;
            }
        }

        private void P2PChatReceivedEventHandler(object sender, P2PChatEventArgs e)
        {
            if(sender is VClient client)
            {
                if (_clients.TryGetValue(client.SocketId, out var chat))
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
                        ControlEvent?.Invoke(file);
                    }
                    else if (e.Type == DataType.Message)
                    {
                        string data = Encoding.UTF8.GetString(e.Data);
                        Label lb = new Label
                        {
                            Text = client.Partner.ComputerName + ": "+ data 
                        };
                        ControlEvent?.Invoke(lb);

                    }
                    else if (e.Type == DataType.AcceptSendFile)
                    {

                    }
                    else if (e.Type == DataType.FileTransfer)
                    {

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
            if (_clients.TryGetValue(id, out var client))
            {
                Label lbChat = new Label
                {
                    Text = client.Client.Partner.ComputerName,
                    Name = client.Client.SocketId,
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
        public void RequestSendFile(FileInfo fileInfo)
        {
            string data = Helpers.StringHelper.StringBuilderWithSeparator("|", fileInfo.Extension, fileInfo.Name, fileInfo.Length);
            byte[] byteArray = Helpers.ByteArrayHelper.ConvertStringToByteArray(data, Enums.EncodingType.UTF8).GetResult();
            SendToClient(DataType.RequestSendFile, byteArray);
        }
        private void SendToClient(DataType type, byte[] data)
        {
            if (string.IsNullOrEmpty(_currentConnectionActivate))
            {
                _currentConnectionActivate = _clients.First().Key;
            }
            if (_clients.TryGetValue(_currentConnectionActivate, out var client))
            {
                client.Client.AddWork(new TaskObject
                {
                    TaskType = type,
                    Data = data,
                    IsSendHeader = true,
                    SessionId = client.Client.SocketId
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
