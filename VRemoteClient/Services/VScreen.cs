using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using VRemoteClient.Models.Entities;
using VRemoteClient.Models.Enums;

namespace VRemoteClient.Services
{
    public class VScreen
    {
        private System.Threading.Timer _timer;
        private BackgroundWorker _backgroundWorker;
        private Queue<ScreenTask> _queueTask;
        private RemoteClient _remoteClient;
        private readonly object _queueLock = new object(); // For thread safety
        public VScreen(RemoteClient client) 
        {
            _remoteClient = client;
            BackgroundWorker = new BackgroundWorker();
            _queueTask = new Queue<ScreenTask>();
            _timer = new System.Threading.Timer(SendScreen, null, 0, (1000 / 1));
        }
        #region Properties
        public BackgroundWorker BackgroundWorker
        {
            get => _backgroundWorker;
            set
            {
                DoWorkEventHandler e = new DoWorkEventHandler(DoWork);
                BackgroundWorker backgroundWorker = this._backgroundWorker;
                if (backgroundWorker != null)
                {
                    backgroundWorker.DoWork -= e;
                }
                _backgroundWorker = value;
                backgroundWorker = _backgroundWorker;
                if (backgroundWorker != null)
                {
                    backgroundWorker.DoWork += e;
                }
            }
        }

        private void DoWork(object sender, DoWorkEventArgs e)
        {
            while (_queueTask.Count > 0)
            {
                bool flag = false;  // Task not completed yet
                ScreenTask taskWork = _queueTask.Dequeue();

                if (taskWork != null)
                {
                    switch (taskWork.WorkType)
                    {
                        case ScreenEnum.FULLSCREEN:
                            SendScreenData(taskWork.Blocks, ref flag);
                            break;
                        case ScreenEnum.REGIONSCREENS:
                            SendChunk(taskWork.Blocks, taskWork.TotalSize, ref flag);
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
        #endregion
        public void SendScreen(object state)
        {
            if (!BackgroundWorker.IsBusy)
            {
                BackgroundWorker.RunWorkerAsync();
            }
            var screens = Utils.Capture.GetScreen();
            if (screens.Any())
            {
                int totalSize = checked(screens.Sum(x => x.TotalSize));
                var task = new ScreenTask
                {
                    WorkType = (screens.Count == 1 && screens[0].IsFullScreen) ? ScreenEnum.FULLSCREEN : ScreenEnum.REGIONSCREENS,
                    Blocks = screens,
                    TotalSize = totalSize
                };
                // Thread-safe enqueue
                lock (_queueLock)
                {
                    _queueTask.Enqueue(task);
                }
            }
        }
        private void SendScreenData(List<ScreenBlock> blocks, ref bool flag)
        {
            if (blocks.Count != 1)
            {
                throw new Exception("Error when send screen");
            }

            //send header before send data
            byte[] header = new byte[5];
            int dataLength = blocks[0].TotalSize;
            Buffer.BlockCopy(BitConverter.GetBytes(dataLength + 1), 0, header, 0, 4); // Add total bytes at the start
            header[4] = (byte)CommandType.Screen; //data type



            //data send
            int CHUNK_SIZE = 1024;

            byte[] bytes = new byte[blocks[0].Bytes.Length];
            //data
            Buffer.BlockCopy(blocks[0].Bytes, 0, bytes, 0, blocks[0].Bytes.Length);//real data

            int numberOfChunk = (int)Math.Ceiling((double)bytes.Length / CHUNK_SIZE);

            for (int i = 0; i < numberOfChunk; i++)
            {
                int offset = i * CHUNK_SIZE;
                int packetSize = Math.Min(CHUNK_SIZE, bytes.Length - i * CHUNK_SIZE);
                byte[] packet = new byte[packetSize];

                //data
                Buffer.BlockCopy(bytes, offset, packet, 0, packetSize);

                _remoteClient.Send(CommandType.None, packet, false);
                Thread.Sleep(1); // Small delay to avoid flooding the network
            }
            flag = true;
        }
        private void SendChunk(List<ScreenBlock> blocks, int totalChunksSize, ref bool flag)
        {
            int CHUNK_SIZE = 1024;
            int numberOfChunk = NumberPacketByTotalSIze(totalChunksSize);
            int data = totalChunksSize + (numberOfChunk * 20);

            Console.WriteLine("ALl chunks data send: " + data);
            byte[] chunks = MergeAllChunk(blocks, data);


            //header
            byte[] header = new byte[5];
            int totalLength = chunks.Length;
            Buffer.BlockCopy(BitConverter.GetBytes(totalLength + 1), 0, header, 0, 4); // Add total bytes at the start
            header[4] = (byte)CommandType.Chunks; //data type


            byte[] bytes = new byte[chunks.Length];

            //data
            Buffer.BlockCopy(chunks, 0, bytes, 5, chunks.Length);    //chunk data

            for (int i = 0; i < numberOfChunk; i++)
            {
                int offset = i * CHUNK_SIZE;
                int packetSize = Math.Min(CHUNK_SIZE, bytes.Length - i * CHUNK_SIZE);
                byte[] packet = new byte[packetSize];

                //data
                Buffer.BlockCopy(bytes, offset, packet, 0, packetSize);

                _remoteClient.Send(CommandType.None, packet, false);
                Thread.Sleep(1); // Small delay to avoid flooding the network
            }
            flag = true;
        }
        private byte[] MergeAllChunk(List<ScreenBlock> cells, int data)
        {
            int offset = 0;
            byte[] chunksData = new byte[data];
            foreach (var chunk in cells)
            {

                Buffer.BlockCopy(BitConverter.GetBytes(chunk.Bytes.Length), 0, chunksData, offset, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(chunk.Rectangle.X), 0, chunksData, offset + 4, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(chunk.Rectangle.Y), 0, chunksData, offset + 8, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(chunk.Rectangle.Width), 0, chunksData, offset + 12, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(chunk.Rectangle.Height), 0, chunksData, offset + 16, 4);

                // Copy chunk data
                Buffer.BlockCopy(chunk.Bytes, 0, chunksData, offset + 20, chunk.Bytes.Length);
                offset += 20 + chunk.Bytes.Length;
            }
            return chunksData;
        }
        private int NumberPacketByTotalSIze(int totalData)
        {
            return (int)Math.Ceiling((double)totalData / 1024);
        }
    }
}
