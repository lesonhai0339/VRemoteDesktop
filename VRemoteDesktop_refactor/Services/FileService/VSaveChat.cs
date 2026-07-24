using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Vsign4.VRemoteDesktop.DTOs;
using Vsign4.VRemoteDesktop.Services.FileService.DTOs;
using Vsign4.VRemoteDesktop.Services.FileService.Enums;
using Vsign4.VRemoteDesktop.Utils;

namespace Vsign4.VRemoteDesktop.Services.FileService
{
    public interface ISaveChat
    {
        /// <summary>
        /// GHi tin nhắn vào file( ghi từng dòng ngay sau khi gửi hoặc nhận, hiện không mã hóa)
        /// </summary>
        /// <param name="msg"></param>
        void Add(ChatMessage msg);
        /// <summary>
        /// Lấy tin nhắn cuối cùng từ file
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        string ReadLastMessage(string filePath);
        /// <summary>
        /// Lấy nhiều tin nhắn từ cuối file
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="numberOfMsg"></param>
        /// <returns></returns>
        string[] ReadLastMessages(string filePath, int numberOfMsg);
        /// <summary>
        /// Lấy nhiều tin nhắn từ cuối file( có thể chứa thông tin file)
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="numberOfMsg"></param>
        /// <returns></returns>
        object[] ReadLastMessagesObject(string filePath, int numberOfMsg);
        void Dispose();
    }
    public class VSaveChat : ISaveChat, IDisposable
    {
        private readonly object _lock = new object();
        private int _disposed = 0;
        // Option 1: using Interlocked
        // 0 = false(Process() not work), 1 = true(Process() is working)
        private int isRunning = 0;

        // Option 2: using volatile bool
        // private volatile bool _isRunning;
        private ManualResetEvent _done = new ManualResetEvent(true);

        private CancellationTokenSource _cancellation = new CancellationTokenSource();
        private ConcurrentQueue<ChatMessage> _chatMessage;
        public VSaveChat()
        {
            isRunning = 0;
            //_isRunning = false;
            _chatMessage = new ConcurrentQueue<ChatMessage>();
        }
        // Stop writing to file.
        // This process starts only when the application launches
        // and cannot be restarted or resumed after stopping,
        // unless the application is fully restarted.
        public void Cancel()
        {
            if(_cancellation != null)
                _cancellation.Cancel();
        }
        //Add message to the write queue
        public void Add(ChatMessage msg)
        {
            _chatMessage.Enqueue(msg);

            //option 1, using Interlocked
            if (Interlocked.CompareExchange(ref isRunning, 1, 0) == 0)
            {
                _done.Reset();
                ThreadPool.QueueUserWorkItem(state => Process());
            }
            //option 2, using volatile bool
            //if (!_isRunning)
            //    Process();
        }
        // Read the last message from filePath
        public string ReadLastMessage(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                //throw new ArgumentNullException("FilePath cannot be null or empty");
                return string.Empty;
            if (!File.Exists(filePath))
                //throw new FileNotFoundException("File not found", filePath);
                return string.Empty;
            try
            {
                using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    if (stream.Length == 0) return string.Empty;
                    var buffer = new List<byte>();
                    long position = stream.Length - 1;
                    while (position >= 0)
                    {
                        stream.Position = position;
                        int currentBytes = stream.ReadByte();
                        if (currentBytes == '\n' || currentBytes == '\r')
                        {
                            if (buffer.Count == 0) break;
                        }
                        else
                        {
                            buffer.Insert(0, (byte)currentBytes);
                        }
                        position--;
                    }
                    return Encoding.UTF8.GetString(buffer.ToArray());
                }
            }
            catch (IOException ex)
            {
                throw;
            }
        }
        //Read multiple messages from filePath
        public string[] ReadLastMessages(string filePath, int numberOfMsg)
        {
            try
            {
                string[] messages = new string[numberOfMsg];
                using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    int count = 0;
                    var buffer = new List<byte>();
                    long position = stream.Length - 1;
                    while (count < numberOfMsg && position >= 0)
                    {
                        stream.Position = position;
                        int currentBytes = stream.ReadByte();
                        if (currentBytes == '\n' || currentBytes == '\r')
                        {
                            if (buffer.Count != 0)
                            {
                                messages[count] = Encoding.UTF8.GetString(buffer.ToArray());
                                buffer.Clear();
                                count++;
                            }
                        }
                        else
                        {
                            buffer.Insert(0, (byte)currentBytes);
                        }
                        position--;
                    }
                    //check if there is remaining buffer to add as last message
                    if (buffer.Count > 0 && count < numberOfMsg)
                    {
                        messages[count] = Encoding.UTF8.GetString(buffer.ToArray());
                    }
                }
                return messages;
            }
            catch (IOException ex)
            {
                throw;
            }
        }
        //Read multiple messages from filePath, including  file info
        public object[] ReadLastMessagesObject(string filePath, int numberOfMsg)
        {
            string[] messages = ReadLastMessages(filePath, numberOfMsg);
            if (messages == null || messages.Length == 0)
                return null;
            var result = messages.Select(x =>
            {
                if (string.IsNullOrEmpty(x))
                    return null;

                ChatFile chatFile;
                if (ParseStringToChatFile(x, out chatFile))
                {
                    return (object)chatFile;
                }
                ChatText chatText;
                if (ParseStringToChatFile(x, out chatText))
                {
                    return (object)chatText;
                }

                return null;
            }).ToArray();
            return result;
        }
        // Parse chat line from file into an object
        private bool ParseStringToChatFile(string rawString, out ChatText chatText)
        {
            chatText = null;

            string[] data = rawString.Split(char.Parse(HeaderSchema.Separator));
            if (data.Length != typeof(ChatText).GetProperties(BindingFlags.Public | BindingFlags.Instance).Length)
                return false;

            ChatContentTypeEnum type;
            if (!Enum.TryParse(data[1], out type))
                return false;
            ChatOwnerEnum owner;
            if (!Enum.TryParse(data[2], out owner))
                return false;
            if (string.IsNullOrWhiteSpace(data[3]))
                return false;
            DateTime time;
            if (!DateTime.TryParseExact(data[0], HeaderSchema.DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
                return false;

            chatText = new ChatText
            {
                Type = type,
                Owner = owner,
                Message = data[3],
                Time = time,
            };
            return true;
        }
        // Parse chat line from file into an object
        private bool ParseStringToChatFile(string rawString, out ChatFile chatFile)
        {
            chatFile = null;

            string[] data = rawString.Split(char.Parse(HeaderSchema.Separator));
            if (data.Length != typeof(ChatFile).GetProperties(BindingFlags.Public | BindingFlags.Instance).Length)
                return false;
            ChatContentTypeEnum type;
            if (!Enum.TryParse(data[1], out type))
                return false;
            ChatOwnerEnum owner;
            if (!Enum.TryParse(data[2], out  owner))
                return false;
            if (string.IsNullOrWhiteSpace(data[3]))
                return false;
            if (string.IsNullOrWhiteSpace(data[4]))
                return false;
            long fileSize = 0;
            if (!long.TryParse(data[5], out fileSize))
                return false;
            DateTime time;
            if (!DateTime.TryParseExact(data[0], HeaderSchema.DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
                return false;

            chatFile = new ChatFile
            {
                Type = type,
                Owner = owner,
                FilePath = data[3],
                FileName = data[4],
                FileSize = fileSize,
                Time = time,

            };
            return true;
        }
        private void Process()
        {
            //case for option 1, using Interlocked
            try
            {
                while (!_cancellation.IsCancellationRequested)
                {
                    ChatMessage item;
                    if (_chatMessage.TryDequeue(out item))
                    {
                        WriteMsgToFile(item);
                    }
                    else
                    {
                        Thread.Sleep(50);
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref isRunning, 0);
                _done.Set();
            }
            //case for option 2, using volatile bool
            //_isRunning = true;
            //    while (_chatMessage.TryDequeue(out var item))
            //    {
            //        WriteMsgToFile(item.FilePath, item.Message);
            //    }
            //_isRunning = false;
        }
        private void WriteMsgToFile(ChatMessage msg)
        {
            try
            {
                string directory = Path.GetDirectoryName(msg.SavePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                lock (_lock)
                {
                    using (StreamWriter write = new StreamWriter(msg.SavePath, true))
                    {
                        if (msg.ChatFile != null)
                            write.WriteLine(msg.ChatFile.ToDataString());
                        else if (msg.ChatText != null)
                            write.WriteLine(msg.ChatText.ToDataString());
                    }
                }
            }
            catch (IOException ex)
            {
                Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "WriteToFile error ");
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;

            if (disposing)
            {
                Cancel();

                _done.WaitOne();
                ChatMessage c;
                while (_chatMessage.TryDequeue(out c)) ;
                _cancellation.Dispose();
                _done.Dispose();
            }
        }
    }
}
