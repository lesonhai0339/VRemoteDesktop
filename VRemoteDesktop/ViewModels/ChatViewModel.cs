using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.Events;
using VRemoteDesktop.Layouts;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.VTCPClient;
using static System.Windows.Forms.LinkLabel;

namespace VRemoteDesktop.ViewModels
{
    public class ChatViewModel: INotifyPropertyChanged, IDisposable
    {
        private readonly Dictionary<string, VClient> _clients;
        private string _clientAdded;
        private string _clientRemoved;
        private string _currentConnectionActivate;
        public ChatViewModel()
        {
            _clients = new Dictionary<string, VClient>();
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
            _clients.Add(key, value);
            value.P2PChatReceived += P2PChatReceivedEventHandler;
            ClientAdded = key;
            _currentConnectionActivate = key;
        }
        public void RemoveConnection(string key)
        {
            if(_clients.TryGetValue(key, out var client))
            {
                client.P2PChatReceived -= P2PChatReceivedEventHandler;
                _clients.Remove(key);
                ClientRemoved = key;
            }
        }

        private void P2PChatReceivedEventHandler(object sender, P2PChatEventArgs e)
        {
            if(sender is VClient client)
            {
                MessageBox.Show($"Received message from connectionID: {client.SocketId} - Message: {Encoding.ASCII.GetString(e.Data)}");
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
        public bool SendChatMessage(string chatData)
        {
            if(_clients.TryGetValue(_currentConnectionActivate, out var client))
            {
                client.AddWork(new TaskObject
                {
                    TaskType = DataType.Message,
                    Data = Encoding.ASCII.GetBytes(chatData),
                    IsSendHeader = true,
                    SessionId = client.SocketId
                });
                return true;
            }
            else
            {
                return false;
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
