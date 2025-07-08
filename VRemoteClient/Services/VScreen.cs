using Serilog;
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
        private const int TIME_OUT = 10;
        private const int CHUNK_SIZE = 8192;
        private BackgroundWorker _backgroundWorker;
        private Queue<ScreenTask> _queueTask;
        private RemoteClient _remoteClient;

        private ManualResetEvent _resetEvent;
        private readonly object _queueLock = new object(); // For thread safety
        private readonly object _lock = new object(); // For thread safety
        public VScreen(RemoteClient client) 
        {
            RemoteClient = client;
            _resetEvent = new ManualResetEvent(false);
            _queueTask = new Queue<ScreenTask>(); 
            BackgroundWorker = new BackgroundWorker();
            BackgroundWorker.RunWorkerAsync();
        }
        #region Properties
        public RemoteClient RemoteClient
        {
            get => _remoteClient;
            set
            {
                RemoteClient client = _remoteClient;
                if (client != null)
                {
                    client.AckEventHandler -= ()=>
                    {
                        _resetEvent.Set(); // Reset the event when an ack is received
                    };
                }
                _remoteClient = value;
                client = _remoteClient;
                if (client != null)
                {
                    client.AckEventHandler += ()=>
                    {
                        _resetEvent.Set(); // Reset the event when an ack is received
                    };
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
                    //header
                    if (screens.Count == 1)
                    {
                        //send header before send data
                        byte[] header = new byte[5];
                        int dataLength = screens[0].TotalSize;
                        Buffer.BlockCopy(BitConverter.GetBytes(dataLength), 0, header, 0, 4); // Add total bytes at the start
                        header[4] = (byte)CommandType.Screen; //data type
                        if (!SendAndWaitAck(CommandType.None, header))
                        {
                            Console.WriteLine("Failed to send header for screen data");
                            return;
                        }
                    }
                    else
                    {
                        int tt = checked(screens.Sum(x => x.TotalSize));
                        int numberOfChunk = NumberPacketByTotalSIze(tt);
                        int data = tt + (numberOfChunk * 20);

                        Console.WriteLine("ALl chunks data send: " + data);
                        byte[] chunks = MergeAllChunk(screens, data);
                        Console.WriteLine("ALl chunks data send: " + data);

                        //header
                        byte[] header = new byte[5];
                        int totalLength = chunks.Length;
                        Buffer.BlockCopy(BitConverter.GetBytes(totalLength), 0, header, 0, 4); // Add total bytes at the start
                        header[4] = (byte)CommandType.Chunks; //data type
                        if (!SendAndWaitAck(CommandType.None, header))
                        {
                            Console.WriteLine("Failed to send header for chunk data");
                            return;
                        }
                    }



                    //data
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

                Thread.Sleep(1000);
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

                    if (!SendAndWaitAck(CommandType.None, packet))
                    {
                        Console.WriteLine($"Failed to send data packet {i + 1}/{numberOfChunk}");
                        return;
                    }
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

                    if (!SendAndWaitAck(CommandType.None, packet))
                    {
                        Console.WriteLine($"Failed to send chunk packet {i + 1}/{numberOfChunk}");
                        return;
                    }
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
        private bool SendAndWaitAck(CommandType cmdType, byte[] data)
        {
            try
            {
                _resetEvent.Reset(); // Reset the event before sending
                RemoteClient.Send(cmdType, data, false);
                bool ackReceived = _resetEvent.WaitOne(1000 * TIME_OUT);

                if (!ackReceived)
                {
                    Console.WriteLine("Timeout waiting for ACK from server. Command: " + cmdType);
                }

                return ackReceived;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending data: {ex.Message}");
                return false;
            }
        }
        [Obsolete("Use SendAndWaitAck instead")]
        private void Send(CommandType cmdType, byte[] data)
        {
            _resetEvent.Reset(); // Reset the event before sending
            RemoteClient.Send(cmdType, data, false);
            bool flag = _resetEvent.WaitOne(1000 * TIME_OUT);
            if (!flag)
            {
                Console.WriteLine("Timeout waiting for ACK from server. Command: " + cmdType);
            }
            _resetEvent.Reset(); // Reset the event after receiving ACK
        }
    }
}
