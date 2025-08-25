using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Services.VTCPClient;

namespace VRemoteDesktop.Models
{
    public class VChat<Source, T>
    {
        public VChat(Source client, List<T> messages)
        {
            Client = client;
            Messages = messages;
        }
        public Source Client { get; set; }
        public List<T> Messages { get; set; }
    }
    public interface IChatManager<Source, T>
    {
        bool Add(string id, Source client);
        bool Remove(string id);
        void AddMessage(string id, T message);
        string GetLastConnectionId();
        Source GetClientById(string id);
        int GetMessageCountById(string id);
        List<T> GetMessages(string id);
        List<T> GetMessages(string id, int offset, int length);
    }
    public class ChatManager<Source, T>: IChatManager<Source, T>
    {
        private readonly ConcurrentDictionary<string, VChat<Source, T>> _curChat;
        public ChatManager()
        {
            _curChat = new ConcurrentDictionary<string, VChat<Source, T>>();
        }
        public bool Add(string id, Source client)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Missing id");
            if (client == null)
                throw new ArgumentException("Client cannot be null");
            
            return _curChat.TryAdd(id, new VChat<Source, T>(
                        client: client,
                        messages: new List<T>())
                   );
        }
        public bool Remove(string id)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("id cannot be null or empty");

            return _curChat.TryRemove(id, out _);
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
        public Source GetClientById(string id)
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
    }
}
