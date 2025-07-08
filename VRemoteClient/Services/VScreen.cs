using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using VRemoteClient.Models.Entities;
using VRemoteClient.Models.Enums;

namespace VRemoteClient.Services
{
    public class VScreen
    {
        private BackgroundWorker _backgroundWorker;
        private Queue<ScreenTask> _queueTask;
        private RemoteClient _remoteClient;
        private readonly object _queueLock = new object(); // For thread safety
        private readonly object _lock = new object(); // For thread safety
        public VScreen(RemoteClient client) 
        {
            _remoteClient = client;
            _queueTask = new Queue<ScreenTask>(); 
            BackgroundWorker = new BackgroundWorker();
            BackgroundWorker.RunWorkerAsync();
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
        #endregion
        private void DoWork(object sender, DoWorkEventArgs e)
        {
            while (true)
            {
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

                    bool flag = false;
                    switch (task.WorkType)
                    {
                        case ScreenEnum.FULLSCREEN:
                            SendScreenData(task.Blocks, ref flag);
                            break;
                        case ScreenEnum.REGIONSCREENS:
                            SendChunk(task.Blocks, task.TotalSize, ref flag);
                            break;
                    }
                }

                Thread.Sleep(1000); // thay cho timer
            }
        }
        private void SendScreenData(List<ScreenBlock> blocks, ref bool flag)
        {
            lock (_lock)
            {
                if (blocks.Count != 1)
                {
                    throw new Exception("Error when send screen");
                }

                //send header before send data
                byte[] header = new byte[5];
                int dataLength = blocks[0].TotalSize;
                Buffer.BlockCopy(BitConverter.GetBytes(dataLength), 0, header, 0, 4); // Add total bytes at the start
                header[4] = (byte)CommandType.Screen; //data type
                _remoteClient.Send(CommandType.None, header, false);



                //data send
                int CHUNK_SIZE = 8192;

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
            }
            flag = true;
        }
        private void SendChunk(List<ScreenBlock> blocks, int totalChunksSize, ref bool flag)
        {
            lock (_lock)
            {
                int CHUNK_SIZE = 8192;
                int numberOfChunk = NumberPacketByTotalSIze(totalChunksSize);
                int data = totalChunksSize + (numberOfChunk * 20);

                Console.WriteLine("ALl chunks data send: " + data);
                byte[] chunks = MergeAllChunk(blocks, data);
                Console.WriteLine("ALl chunks data send: " + data);

                //header
                byte[] header = new byte[5];
                int totalLength = chunks.Length;
                Buffer.BlockCopy(BitConverter.GetBytes(totalLength), 0, header, 0, 4); // Add total bytes at the start
                header[4] = (byte)CommandType.Chunks; //data type
                _remoteClient.Send(CommandType.None, header, false);


                //data
                byte[] bytes = new byte[chunks.Length];
                Buffer.BlockCopy(chunks, 0, bytes, 0, chunks.Length);    //chunk data

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
            }
            flag = true;
        }
        private byte[] MergeAllChunk(List<ScreenBlock> cells, int data)
        {
            using (var ms = new MemoryStream())
            {
                foreach (var chunk in cells)
                {
                    ms.Write(BitConverter.GetBytes(chunk.Bytes.Length), 0, 4);
                    ms.Write(BitConverter.GetBytes(chunk.Rectangle.X), 0, 4);
                    ms.Write(BitConverter.GetBytes(chunk.Rectangle.Y), 0, 4);
                    ms.Write(BitConverter.GetBytes(chunk.Rectangle.Width), 0, 4);
                    ms.Write(BitConverter.GetBytes(chunk.Rectangle.Height), 0, 4);
                    ms.Write(chunk.Bytes, 0, chunk.Bytes.Length);
                }
                return ms.ToArray();
            }
        }
        private int NumberPacketByTotalSIze(int totalData)
        {
            return (int)Math.Ceiling((double)totalData / 8192);
        }
    }
}
