using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace VRemoteDesktop.Services.FileService
{
    public class ChatMessageData
    {
        public ChatMessageData(string filePath, string message)
        {
            FilePath = filePath;
            Message = message;
        }
        public string FilePath { get; set; }
        public string Message { get; set; }
    }
    public class SaveChat
    {
        private readonly object _lock = new object();

        // Option 1: using Interlocked
        // 0 = false(Process() not work), 1 = true(Process() is working)
        private int isRunning = 0;

        // Option 2: using volatile bool
        // private volatile bool _isRunning;

        private ConcurrentQueue<ChatMessageData> _chatMessage;
        public SaveChat()
        {
            isRunning = 0;
            //_isRunning = false;
            _chatMessage = new ConcurrentQueue<ChatMessageData>();
        }
        /// <summary>
        /// Add message to queue to save to file and avoid missing message when multiple thread try to write to file at the same time
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="message"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public void Add(string filePath, string message)
        {
            if(string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException("FilePath cannot be null or empty");
            if(string.IsNullOrWhiteSpace(message))
                throw new ArgumentNullException("Message cannot be null or empty");

            _chatMessage.Enqueue(new ChatMessageData(filePath: filePath, message: message));

            //option 1, using Interlocked
            if (Interlocked.CompareExchange(ref isRunning, 1, 0) == 0)
            {
                ThreadPool.QueueUserWorkItem(state => Process());
            }
            //option 2, using volatile bool
            //if (!_isRunning)
            //    Process();
        }
        /// <summary>
        /// Read last message from the end of the file  
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        public string ReadLastMessage(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException("FilePath cannot be null or empty");
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found", filePath);
            try
            {
                using(FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    if (stream.Length == 0) return string.Empty;
                    var buffer = new List<byte>();
                    long position = stream.Length - 1;
                    while(position >= 0)
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
            catch(IOException ex)
            {
                throw;
            }
        }
        /// <summary>
        /// Read multi messages from the end of the file
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="numberOfMsg"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        public string[] ReadLastMessages(string filePath, int numberOfMsg)
        {
            if(string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException("FilePath cannot be null or empty");
            if (numberOfMsg <=0)
                throw new ArgumentOutOfRangeException("Number of message must be greater than zero");
            if(!File.Exists(filePath))
                throw new FileNotFoundException("File not found", filePath);

            try
            {
                string[] messages = new string[numberOfMsg];
                using(FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    int count = 0;
                    var buffer = new List<byte>();
                    long position = stream.Length - 1;
                    while (count < numberOfMsg && position >= 0)
                    {
                        stream.Position = position;
                        int currentBytes = stream.ReadByte();
                        if(currentBytes == '\n' || currentBytes == '\r')
                        {
                            if(buffer.Count != 0)
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
            catch(IOException ex)
            {
                throw;
            }
        }
        private void Process()
        {
            //case for option 1, using Interlocked
            try
            {
                while (_chatMessage.TryDequeue(out var item))
                {
                    WriteMsgToFile(item.FilePath, item.Message);
                }
            }
            finally
            {
                Interlocked.Exchange(ref isRunning, 0);
            }
            //case for option 2, using volatile bool
            //_isRunning = true;
            //    while (_chatMessage.TryDequeue(out var item))
            //    {
            //        WriteMsgToFile(item.FilePath, item.Message);
            //    }
            //_isRunning = false;
        }
        private void WriteMsgToFile(string filePath, string message)
        {
            try
            {
                lock (_lock)
                {
                    using(StreamWriter write = new StreamWriter(filePath, true))
                    {
                        write.WriteLine(message);
                        write.Flush();
                    }
                }
            }
            catch(IOException ex)
            {
                throw;
            }
        }
    }
}
