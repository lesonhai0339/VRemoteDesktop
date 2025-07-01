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
using System.Windows.Forms;
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
        public ClientEnum WorkType { get; set; }
        public List<CaptureCell> Cells { get; set; }
        public int TotalSize { get; set; }
    }
    public class ClientClass
    {
        private System.Threading.Timer _timer;
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
            _timer = new System.Threading.Timer(SendScreen, null, 0, (1000 / 10));
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
                    switch (taskWork.WorkType)
                    {
                        case ClientEnum.FULLSCREEN:
                            SendScreenData(taskWork.Cells, ref flag);
                            break;
                        case ClientEnum.REGIONSCREENS:
                            SendChunk(taskWork.Cells, taskWork.TotalSize, ref flag);
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
                int totalSize = checked(screens.Sum(x => x.TotalSize));
                var task = new TaskWork
                {
                    WorkType = (screens.Count == 1 && screens[0].IsFullScreen) ? ClientEnum.FULLSCREEN : ClientEnum.REGIONSCREENS,
                    Cells = screens,
                    TotalSize = totalSize
                };
                // Thread-safe enqueue
                lock (_queueLock)
                {
                    QueueTask.Enqueue(task);
                }
            }
        }
        private void SendScreenData(List<CaptureCell> cells, ref bool flag)
        {
            if(cells.Count != 1)
            {
                throw new Exception("Error when send screen");
            }
            int CHUNK_SIZE = 1024;

            byte[] bytes = new byte[cells[0].Bytes.Length + 9];

            Array.Copy(BitConverter.GetBytes(cells[0].Bytes.Length + 1), 0, bytes, 0, 4); // Add total bytes at the start

            //caculate padding need to add
            int lastChunkSize = bytes.Length % CHUNK_SIZE;
            int padding = 0;
            if (lastChunkSize != 0)
            {
                padding = CHUNK_SIZE - lastChunkSize;
            }
            Array.Copy(BitConverter.GetBytes(padding), 0, bytes, 4, 4); //padding added when not enough 1024 bytes

            bytes[8] = 4; //data type

            //data
            Array.Copy(cells[0].Bytes, 0, bytes, 9, cells[0].Bytes.Length);//real data

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
        private void SendChunk(List<CaptureCell> cells, int totalChunksSize, ref bool flag)
        {
            int CHUNK_SIZE = 1024;
            int numberOfChunk = NumberPacketByTotalSIze(totalChunksSize);
            int data = totalChunksSize + (numberOfChunk * 20);

            Console.WriteLine("ALl chunks data send: "+ data);
            byte[] chunks = MergeAllChunk(cells, data);


            byte[] bytes = new byte[chunks.Length + 9];    //9 bytes for common headers

            Array.Copy(BitConverter.GetBytes(chunks.Length + 1), 0, bytes, 0, 4); // Add total bytes of current chunk

            //caculate padding need to add
            int lastChunkSize = bytes.Length % CHUNK_SIZE;
            int padding = 0;
            if (lastChunkSize != 0)
            {
                padding = CHUNK_SIZE - lastChunkSize;
            }
            Array.Copy(BitConverter.GetBytes(padding), 0, bytes, 4, 4);  //padding added to packet enough 1024 bytes

            bytes[8] = 5; //send chunk

            //data
            Array.Copy(chunks, 0, bytes, 9, chunks.Length);    //chunk data

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
        private byte[] MergeAllChunk(List<CaptureCell> cells,int data)
        {
            byte[] chunksData = new byte[data];
            foreach(var chunk in cells)
            {
                Array.Copy(BitConverter.GetBytes(chunk.TotalSize), 0, chunksData, 0, 4);  //chunk length

                int x = chunk.Rectangle.X;   //rectangle x
                int y = chunk.Rectangle.Y;   //rectangle y
                int width = chunk.Rectangle.Width;   //rectangle width
                int height = chunk.Rectangle.Height; //rectangle height
                Array.Copy(BitConverter.GetBytes(x), 0, chunksData, 4, 4);  //4 bytes 
                Array.Copy(BitConverter.GetBytes(y), 0, chunksData, 8, 4);  //4 bytes
                Array.Copy(BitConverter.GetBytes(width), 0, chunksData, 12, 4);  //4 bytes
                Array.Copy(BitConverter.GetBytes(height), 0, chunksData, 16, 4); //4 bytes

                //chunk data
                Array.Copy(chunk.Bytes, 0 , chunksData, 20, chunk.TotalSize);
            }
            return chunksData;
        }
        private int NumberPacketByTotalSIze(int totalData)
        {
            return (int)Math.Ceiling((double)totalData / 1024);
        }
        /* private void SendChunk(CaptureCell cell,int totalChunksSize, ref bool flag)
         {
             Console.WriteLine($"Total chunks size: {totalChunksSize}");
             int CHUNK_SIZE = 1024;

             byte[] bytes = new byte[cell.Bytes.Length + 29];    //29 bytes for headers

             Array.Copy(BitConverter.GetBytes(cell.Bytes.Length + 21), 0, bytes, 0, 4); // Add total bytes of current chunk

             //caculate padding need to add
             int lastChunkSize = bytes.Length % CHUNK_SIZE;
             int padding = 0;
             if (lastChunkSize != 0)
             {
                 padding = CHUNK_SIZE - lastChunkSize;
             }
             Array.Copy(BitConverter.GetBytes(padding), 0, bytes, 4, 4);  //padding added to packet enough 1024 bytes

             bytes[8] = 5; //send chunk

             Array.Copy(BitConverter.GetBytes(totalChunksSize), 0, bytes, 9, 4);  // add total bytes of all chunks

             int x = cell.Rectangle.X;   //rectangle x
             int y = cell.Rectangle.Y;   //rectangle y
             int width = cell.Rectangle.Width;   //rectangle width
             int height = cell.Rectangle.Height; //rectangle height
             Array.Copy(BitConverter.GetBytes(x), 0, bytes, 13, 4);  //4 bytes 
             Array.Copy(BitConverter.GetBytes(y), 0, bytes, 17, 4);  //4 bytes
             Array.Copy(BitConverter.GetBytes(width), 0, bytes, 21, 4);  //4 bytes
             Array.Copy(BitConverter.GetBytes(height), 0, bytes, 25, 4); //4 bytes

             //remaining data
             Array.Copy(cell.Bytes, 0, bytes, 29, cell.Bytes.Length);    //chunk data

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
         }*/
        #region Events
        #endregion
        #endregion
    }
}
