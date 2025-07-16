using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VRemoteClient.Models.Entities;
using VRemoteClient.Models.Enums;

namespace VRemoteClient.Services
{
    public class ScreenHook
    {
        private const int TIME_OUT = 10;
        private const int CHUNK_SIZE = 8192;

        private bool _isSendSuccessed = false;
        private byte[] _buffer = new byte[20];
        private byte[] _packet = new byte[CHUNK_SIZE];
        private byte[] _dataSend;


        private BackgroundWorker _backgroundWorker;
        private RemoteClient _remoteClient;
        private ManualResetEvent _resetEvent;
        private readonly object _lock = new object(); // For thread safety
        public ScreenHook(RemoteClient client) 
        {
            var bounds = Screen.PrimaryScreen.Bounds;
            int pixelCount = bounds.Width * bounds.Height;
            int bufferSize = pixelCount > 3840000 ? 30 * 1024 * 1024 : 10 * 1024 * 1024;
            _dataSend = new byte[bufferSize];

            RemoteClient = client;
            _resetEvent = new ManualResetEvent(false);
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
                    client.ChunksSuccessEventHandler -= SentResponse;
                    client.ScreenSuccessEventHandler -= SentResponse;
                }
                _remoteClient = value;
                client = _remoteClient;
                if (client != null)
                {
                    client.ChunksSuccessEventHandler += SentResponse;
                    client.ScreenSuccessEventHandler += SentResponse;
                }
            }
        }

        private void SentResponse(bool flag)
        {
            _isSendSuccessed = flag;
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
                _isSendSuccessed = false;
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                var screens = Utils.Capture.GetScreen();
                if (screens.Any())
                {
                    int totalSize = checked(screens.Sum(x => x.TotalSize));
                    ScreenEnum screenEnum = (screens.Count == 1 && screens[0].IsFullScreen) ? ScreenEnum.FULLSCREEN : ScreenEnum.REGIONSCREENS;
                    switch (screenEnum)
                    {
                        case ScreenEnum.FULLSCREEN:
                            SendScreenData(screens);
                            break;
                        case ScreenEnum.REGIONSCREENS:
                            SendChunk(screens, totalSize);
                            break;
                    }
                }
                else
                {
                    _isSendSuccessed = true;
                }
                while (!_isSendSuccessed)
                {
                    Thread.Sleep(10);
                }
                stopwatch.Stop();
                Console.WriteLine("Elapsed time: "+ stopwatch.Elapsed.TotalMilliseconds);
            }
        }
        // Send full screen to sender when first connect
        private void SendScreenData(List<ScreenBlock> blocks)
        {
            try
            {
                lock (_lock)
                {
                    if (blocks.Count != 1)
                    {
                        Log.ForContext("Screen", "RemoteDesktopClient")
                                          .Error($"Blocks number more than expected");
                        return;
                    }
                    byte[] screenCompressed = Utils.Extensions.CompressGzip(blocks[0].Bytes);
                    int dataLength = screenCompressed.Length + 5;

                    //header
                    Buffer.BlockCopy(BitConverter.GetBytes(dataLength), 0, _dataSend, 0, 4); // Add total bytes at the start
                    _dataSend[4] = (byte)CommandType.Screen; //data type


                    //data
                    Buffer.BlockCopy(screenCompressed, 0, _dataSend, 5, screenCompressed.Length);//real data

                    int numberOfChunk = (int)Math.Ceiling((double)dataLength / CHUNK_SIZE);

                    for (int i = 0; i < numberOfChunk; i++)
                    {
                        int offset = i * CHUNK_SIZE;
                        int packetSize = Math.Min(CHUNK_SIZE, dataLength - i * CHUNK_SIZE);

                        //data
                        Buffer.BlockCopy(_dataSend, offset, _packet, 0, packetSize);

                        if (!SendAndWaitAck(CommandType.None, _packet, packetSize))
                        {
                            //Console.WriteLine($"Failed to send data packet {i + 1}/{numberOfChunk}");
                            return;
                        }
                        Thread.Sleep(1); // Small delay to avoid flooding the network
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine("Screen: " + ex.Message);
            }
        }
        //Capture and send region change to sender
        private void SendChunk(List<ScreenBlock> blocks, int totalChunksSize)
        {
            try
            {
                lock (_lock)
                {
                    byte[] sourceChunks = MergeAllChunk(blocks);
                    byte[] chunks = Utils.Extensions.CompressGzip(sourceChunks);

                    //headers always 5 bytes, 4 bytes for data length and 1 byte for command type
                    int numberOfChunk = (chunks.Length + 5 + 8191) / 8192; // NumberPacketByTotalSIze(chunks.Length + 5);
                    int totalLength = chunks.Length;

                    int dataSendLength = totalLength + 5;

                    //header
                    //Buffer.BlockCopy(BitConverter.GetBytes(totalLength + 5), 0, dataSend, 0, 4); // Set total bytes at the start
                    //dataSend[4] = (byte)CommandType.Chunks; // Set command type at offset 4
                    unsafe
                    {
                        fixed (byte* ptr = _dataSend)
                        {
                            *(int*)ptr = dataSendLength; // Set total bytes at the start
                            *(ptr + 4) = (byte)CommandType.Chunks; // Set command type at offset 4
                        }
                    }

                    //data
                    Buffer.BlockCopy(chunks, 0, _dataSend, 5, totalLength);    //chunk data


                    //cut data to chunk(8192 bytes)  and send
                    for (int i = 0; i < numberOfChunk; i++)
                    {
                        int offset = i * CHUNK_SIZE;
                        int remain = dataSendLength - offset;

                        int packetSize = Math.Min(CHUNK_SIZE, remain);

                        //data
                        Buffer.BlockCopy(_dataSend, offset, _packet, 0, packetSize);

                        if (!SendAndWaitAck(CommandType.None, _packet, packetSize))
                        {
                            //Console.WriteLine($"Failed to send chunk packet {i + 1}/{numberOfChunk}");
                            return;
                        }
                        Thread.Sleep(1); // Small delay to avoid flooding the network
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine("Chunks error: " + ex.Message);
            }
        }
        // Merge all chunks into a single byte array
        private unsafe byte[] MergeAllChunk(List<ScreenBlock> blocks)
        {
            using (var ms = new MemoryStream())
            {
                int count = blocks.Count;

                for (int i = 0; i< count; i++)
                {

                    fixed (byte* p = _buffer)
                    {
                        int* pInt = (int*)p;
                        pInt[0] = blocks[i].Bytes.Length; // Length of the chunk
                        pInt[1] = blocks[i].Rectangle.X; // X coordinate of the rectangle
                        pInt[2] = blocks[i].Rectangle.Y; // Y coordinate of the rectangle
                        pInt[3] = blocks[i].Rectangle.Width; // Width of the rectangle
                        pInt[4] = blocks[i].Rectangle.Height; // Height of the rectangle

                        //note: can write like this *(pInt + 1) = blocks[i].Rectangle.X; 
                    }
                    ms.Write(_buffer, 0, _buffer.Length); // Write the header
                    ms.Write(blocks[i].Bytes, 0 , blocks[i].Bytes.Length); // Write the chunk data
                    //ms.Write(BitConverter.GetBytes(blocks[i].Bytes.Length), 0, 4);
                    //ms.Write(BitConverter.GetBytes(blocks[i].Rectangle.X), 0, 4);
                    //ms.Write(BitConverter.GetBytes(blocks[i].Rectangle.Y), 0, 4);
                    //ms.Write(BitConverter.GetBytes(blocks[i].Rectangle.Width), 0, 4);
                    //ms.Write(BitConverter.GetBytes(blocks[i].Rectangle.Height), 0, 4);
                    //ms.Write(blocks[i].Bytes, 0, blocks[i].Bytes.Length);
                }
                return ms.ToArray();
            }
        }
        private bool SendAndWaitAck(CommandType cmdType, byte[] data, int sendLength)
        {
            // _resetEvent.Reset(); // Reset the event before sending
            RemoteClient.Send(commandType: cmdType, data: data, sendLength: sendLength);
            //bool ackReceived = _resetEvent.WaitOne(1000 * TIME_OUT);

            //if (!ackReceived)
            //{
            //    Console.WriteLine("Timeout waiting for ACK from server. Command: " + cmdType);
            //}
            //return ackReceived;
            return true;
        }
        [Obsolete("")]
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
