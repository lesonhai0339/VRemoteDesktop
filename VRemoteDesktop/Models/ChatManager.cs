using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Events;
using VRemoteDesktop.Services.VTCPClient;

namespace VRemoteDesktop.Models
{
    public class VChat<T>
    {
        public VChat(VClient client, List<T> messages)
        {
            Client = client;
            Messages = messages;
        }
        public VClient Client { get; set; }
        public List<T> Messages { get; set; }
    }
    public interface IChatManager<T>
    {
        bool Add(string id, VClient client);
        bool Remove(string id);
        void AddMessage(string id, T message);
        string GetLastConnectionId();
        VClient GetClientById(string id);
        int GetMessageCountById(string id);
        List<T> GetMessages(string id);
        List<T> GetMessages(string id, int offset, int length);
        event EventHandler<ChatDisconnectedEventArgs> ChatDisconnected;
        void Dispose();
    }
    public class ChatManager<T>: IChatManager<T>, IDisposable
    {
        private readonly ConcurrentDictionary<string, VChat<T>> _curChat;
        public event EventHandler<ChatDisconnectedEventArgs> ChatDisconnected;
        public ChatManager()
        {
            _curChat = new ConcurrentDictionary<string, VChat<T>>();
        }
        public bool Add(string id, VClient client)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Missing id");
            if (client == null)
                throw new ArgumentException("Client cannot be null");

            client.SocketDisposing += SocketDisposingEventHandler;
            return _curChat.TryAdd(id, new VChat<T>(
                        client: client,
                        messages: new List<T>())
                   );
        }
        public bool Remove(string id)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("id cannot be null or empty");

            if(_curChat.TryGetValue(id, out var client))
            {
                client.Client.SocketDisposing -= SocketDisposingEventHandler;
                return _curChat.TryRemove(id, out _);
            }
            return false;
        }
        public void AddMessage(string id, T message)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("id cannot be null or empty");
            if (message == null)
                throw new ArgumentException("message cannot be null");

            if(_curChat.TryGetValue(id, out var chat))
            {
                chat.Messages.Add(message);
            }
            else
            {
                throw new InvalidOperationException("Cannot find Chat connection with id: " + id);
            }
        }
        public string GetLastConnectionId()
        {
            return _curChat.Last().Key;
        }
        public VClient GetClientById(string id)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Missing id");
            if(_curChat.TryGetValue(id, out var chat))
            {
                return chat.Client;
            }
            else
            {
                throw new InvalidOperationException("Cannot find client");
            }
        }
        public int GetMessageCountById(string id)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Missing id");

            if(_curChat.TryGetValue(id, out var chat))
            {
                return chat.Messages.Count;
            }
            else
            {
                throw new InvalidOperationException("Cannot find chat connection with id:  " + id);
            }
        }
        public List<T> GetMessages(string id)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Missing id");

            if (_curChat.TryGetValue(id, out var chat))
            {
                return chat.Messages;
            }
            else
            {
                throw new InvalidOperationException("Cannot find chat connection with id:  " + id);
            }
        }
        public List<T> GetMessages(string id, int offset, int length)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Missing id");
            if (offset < 0)
                throw new ArgumentException("Offset cannot be negative");
            if (length <= 0)
                throw new ArgumentException("Length cannot be 0 or negative");

            if (_curChat.TryGetValue(id, out var chat))
            {
                if (offset >= chat.Messages.Count)
                    return new List<T>();

                int num = Math.Min(length, chat.Messages.Count - offset);
                return chat.Messages.GetRange(offset, num);
            }
            else
            {
                throw new InvalidOperationException("Cannot find chat connection with id:  " + id);
            }
        }
        private void SocketDisposingEventHandler(object sender, SocketDisposeEventArgs e)
        {
            if(sender is VClient client)
            {
                Remove(client.SocketId);
                ChatDisconnected?.Invoke(this, new ChatDisconnectedEventArgs(client.SocketId));
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
                foreach(var item in _curChat)
                {
                    Remove(item.Key);
                }
                _curChat.Clear();
            }
        }
    }
}
