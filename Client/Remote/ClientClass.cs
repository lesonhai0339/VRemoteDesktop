using Client.FFmpeg.ScreenA;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Remoting.Lifetime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace RemoteClient.Remote
{
    public enum ClientEnum
    {
        None = 0,
        FULLSCREEN = 1,
        REGIONSCREENS = 2,
    }
    public class TaskWork
    {
        public ClientEnum workType;
        public byte[] data;
        public CaptureCell cell;
    }
    public class ClientClass
    {
        private Timer _timer;
        private BackgroundWorker _backgroundWorker;
        private Queue<TaskWork> _queueTask;
        private SocketRemoteClient _remoteClient;
        private ConnectionInfo _connectionInfo;
        private readonly object _queueLock = new object(); // For thread safety
        public ClientClass(SocketRemoteClient remoteCLient, ConnectionInfo info)
        {
            Client = remoteCLient;
            QueueTask = new Queue<TaskWork>();
            _connectionInfo = info;


            BackgroundWorker = new BackgroundWorker();
            _timer = new Timer(SendScreen, null, 0, (1000 / 10));
        }
        #region Properties
        public SocketRemoteClient Client
        {
            get=> _remoteClient;
            set
            {
                if(_remoteClient != null)
                {
                }
                _remoteClient = value;
                if(_remoteClient != null)
                {
                }
            }
        }
        public BackgroundWorker BackgroundWorker
        {
            get => _backgroundWorker;
            set
            {
                DoWorkEventHandler e = new DoWorkEventHandler(DoWork);
                BackgroundWorker backgroundWorker = this._backgroundWorker;
                if(backgroundWorker != null)
                {
                    backgroundWorker.DoWork -= e;
                }
                _backgroundWorker = value;
                backgroundWorker = _backgroundWorker;
                if(backgroundWorker != null)
                {
                    backgroundWorker.DoWork += e;
                }
            }
        }
        public Queue<TaskWork> QueueTask
        {
            get => _queueTask;
            private set
            {
                _queueTask = value;
            }
        }
        #endregion
        #region Methods
        private void DoWork(object sender, DoWorkEventArgs e)
        {
            while (QueueTask.Count > 0)
            {
                bool flag = false;  // Task not completed yet
                TaskWork taskWork = QueueTask.Dequeue();

                if (taskWork != null)
                {
                    switch (taskWork.workType)
                    {
                        case ClientEnum.FULLSCREEN:
                            SendScreenData(taskWork.cell, ref flag);
                            break;
                        case ClientEnum.REGIONSCREENS:
                            SendChunk(taskWork.cell, ref flag);
                            break;
                        default:
                            flag = true; // Skip unknown types
                            break;
                    }

                    // Wait until the task is completed
                    while (!flag)
                    {
                        Thread.Sleep(10);
                    }
                }

                Thread.Sleep(10); // Small delay between tasks
            }
        }
        public void SendScreen(object state)
        {
            if (!BackgroundWorker.IsBusy)
            {
                BackgroundWorker.RunWorkerAsync();
            }
            var screens = CaptureScreen.GetScreen();
            if (screens.Any())
            {
                var tasks = new List<TaskWork>();
                Parallel.ForEach(screens, screen =>
                {
                    var task = new TaskWork
                    {
                        workType = screen.IsFullScreen ?  ClientEnum.FULLSCREEN : ClientEnum.REGIONSCREENS,
                        cell = screen,
                        data = null
                    };
                    lock (tasks)
                    {
                        tasks.Add(task);
                    }
                });
                // Thread-safe enqueue
                lock (_queueLock)
                {
                    foreach (var task in tasks)
                    {
                        QueueTask.Enqueue(task);
                    }
                }
            }
        }
        private void SendScreenData(CaptureCell cell, ref bool flag)
        {
            int CHUNK_SIZE = 1024;

            byte[] bytes = new byte[cell.Bytes.Length + 9];

            Array.Copy(BitConverter.GetBytes(cell.Bytes.Length + 1), 0, bytes, 0, 4); // Add total bytes at the start

            //caculate padding need to add
            int lastChunkSize = bytes.Length % CHUNK_SIZE;
            int padding = 0;
            if (lastChunkSize != 0)
            {
                padding = CHUNK_SIZE - lastChunkSize;
            }
            Array.Copy(BitConverter.GetBytes(padding), 0, bytes, 4, 4);

            bytes[8] = 4;

            //data
            Array.Copy(cell.Bytes, 0, bytes, 9, cell.Bytes.Length);//real data

            int numberOfChunk = (int)Math.Ceiling((double)bytes.Length / CHUNK_SIZE);

            Console.WriteLine($"Screen : {bytes.Length - 9}");

            for (int i = 0; i < numberOfChunk; i++)
            {
                int offset = i * CHUNK_SIZE;
                int packetSize = Math.Min(CHUNK_SIZE, bytes.Length - i * CHUNK_SIZE);
                byte[] packet = new byte[packetSize];

                //data
                Array.Copy(bytes, offset, packet, 0, packetSize);

                 if (((i + 1) % 5) == 0)
                {
                    Thread.Sleep(1);
                }
                Client.SendData(Enums.DataType.P2PDATASEND, packet);
            }
            flag = true;
        }
        private void SendChunk(CaptureCell cell, ref bool flag)
        {
            int CHUNK_SIZE = 1024;

            byte[] bytes = new byte[cell.Bytes.Length + 25];

            Array.Copy(BitConverter.GetBytes(cell.Bytes.Length + 17), 0, bytes, 0, 4); // Add total bytes at the start

            //caculate padding need to add
            int lastChunkSize = bytes.Length % CHUNK_SIZE;
            int padding = 0;
            if (lastChunkSize != 0)
            {
                padding = CHUNK_SIZE - lastChunkSize;
            }
            Array.Copy(BitConverter.GetBytes(padding), 0, bytes, 4, 4);

            bytes[8] = 5; //send chunk


            int x = cell.Rectangle.X;
            int y = cell.Rectangle.Y;
            int width = cell.Rectangle.Width;
            int height = cell.Rectangle.Height;
            Array.Copy(BitConverter.GetBytes(x), 0, bytes, 9, 4);//4 bytes
            Array.Copy(BitConverter.GetBytes(y), 0, bytes, 13, 4);//4 bytes
            Array.Copy(BitConverter.GetBytes(width), 0, bytes, 17, 4);//4 bytes
            Array.Copy(BitConverter.GetBytes(height), 0, bytes, 21, 4);//4 bytes

            //remaining data
            Array.Copy(cell.Bytes, 0, bytes, 25, cell.Bytes.Length);//real data

            int numberOfChunk = (int)Math.Ceiling((double)bytes.Length / CHUNK_SIZE);

            Console.WriteLine($"Chunk : {bytes.Length - 9}");

            for (int i = 0; i < numberOfChunk; i++)
            {
                int offset = i * CHUNK_SIZE;
                int packetSize = Math.Min(CHUNK_SIZE, bytes.Length - i * CHUNK_SIZE);
                byte[] packet = new byte[packetSize];

                //data
                Array.Copy(bytes, offset, packet, 0, packetSize);
                if (((i + 1) % 5) == 0)
                {
                    Thread.Sleep(1);
                }
                Client.SendData(Enums.DataType.P2PDATASEND, packet);
            }
            flag = true;
        }
        #region Events
        #endregion
        #endregion
    }
}
