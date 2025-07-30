using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using VRemoteClient.Models.CustomEvents;
using VRemoteClient.Models.Entities;
using VRemoteClient.Models.Enums;
using VRemoteClient.Services.ConnectionService;
using VRemoteClient.Utils;

namespace VRemoteClient.Services.ScreenService
{
    public class GlobalScreenCapture: IDisposable
    {
        private const int TIME_OUT = 10;
        private const int CHUNK_SIZE = 8192;
        private readonly object _lock = new object(); // For thread safety. Can use ReadWriteLockSlim instead

        private bool _disposed = false;
        private byte[] _buffer = new byte[20];
        private byte[] _dataSend;

        private ScreenCapture _capture;
        private BackgroundWorker _backgroundWorker;
        public event EventHandler<CustomScreenEventArgs> ScreenEvent;
        private CancellationTokenSource _cancel = new CancellationTokenSource();
        public GlobalScreenCapture()
        {
            var bounds = Screen.PrimaryScreen.Bounds;
            int pixelCount = bounds.Width * bounds.Height;
            int bufferSize = pixelCount > 3840000 ? 30 * 1024 * 1024 : 10 * 1024 * 1024;
            _dataSend = new byte[bufferSize];
            _capture = new ScreenCapture();
            BackgroundWorker = new BackgroundWorker();
            BackgroundWorker.WorkerSupportsCancellation = true;
        }
        #region Properties
        public bool IsCapturing
        {
            get => BackgroundWorker.IsBusy;
        }
        public bool IsDisposed
        {
            get => _disposed;
        }
        public BackgroundWorker BackgroundWorker
        {
            get => _backgroundWorker;
            set
            {
                DoWorkEventHandler e = new DoWorkEventHandler(DoWork);
                BackgroundWorker backgroundWorker = _backgroundWorker;
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
        public void StartCapture()
        {
            _capture.Renew();
            if (!BackgroundWorker.IsBusy)
            {
                _cancel?.Dispose();
                _cancel = new CancellationTokenSource();
                BackgroundWorker.RunWorkerAsync();
                Log.ForContext("Screen", "RemoteDesktopClient")
                                         .Info($"Start capture");
            }
        }
        public void StopCapture()
        {
            if (BackgroundWorker.IsBusy)
            {
                _cancel?.Cancel();
                BackgroundWorker.CancelAsync();
                Log.ForContext("Screen", "RemoteDesktopClient")
                                                         .Info($"Stop capture");
            }
        }
        private void DoWork(object sender, DoWorkEventArgs e)
        {
            while (!_cancel.IsCancellationRequested)
            {
                if(!(ConnectionManager.NumberOfConnections > 0))
                {
                    Log.ForContext("Screen", "RemoteDesktopClient")
                                         .Info($"No connection, screen capture will be stopping");
                    StopCapture();
                }
                var screens = _capture.GetScreen();
                if (screens.Any())
                {
                    int totalSize = checked(screens.Sum(x => x.TotalSize));
                    ScreenEnum screenEnum = screens.Count == 1 && screens[0].IsFullScreen ? ScreenEnum.FULLSCREEN : ScreenEnum.REGIONSCREENS;
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
                Thread.Sleep(10);
            }
        }
        // Send full screen to sender at first connect
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
                    byte[] screenCompressed = Extensions.CompressGzip(blocks[0].Bytes);
                    byte[] screenHashed = Encoding.ASCII.GetBytes(Extensions.SHAHash(screenCompressed));
                    int dataLength = screenCompressed.Length + screenHashed.Length;

                    //checksum
                    Buffer.BlockCopy(screenHashed, 0, _dataSend, 0, screenHashed.Length);

                    //data compressed
                    Buffer.BlockCopy(screenCompressed, 0, _dataSend, screenHashed.Length, screenCompressed.Length);

                    int numberOfChunk = (int)Math.Ceiling((double)dataLength / CHUNK_SIZE);

                    CustomScreenEventArgs screen = new CustomScreenEventArgs(
                        type: RemoteType.Screen,
                        totalSize: dataLength
                    );
                    for (int i = 0; i < numberOfChunk; i++)
                    {
                        int offset = i * CHUNK_SIZE;
                        int packetSize = Math.Min(CHUNK_SIZE, dataLength - i * CHUNK_SIZE);

                        // Note: Cannot use a shared buffer here because Send() adds the task to a queue.
                        // If a shared buffer is used, the next packet may overwrite the previous data,
                        // causing all queued packets to contain the same (last) data.
                        byte[] chunkData = new byte[packetSize];
                        //data
                        Buffer.BlockCopy(_dataSend, offset, chunkData, 0, packetSize);
                        screen.Data.Add(chunkData);
                    }

                    ScreenEvent?.Invoke(null,screen);
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "ScreenHook").Error(ex, "Screen event error");
            }
        }
        //Capture and send screen region change to sender
        private void SendChunk(List<ScreenBlock> blocks, int totalChunksSize)
        {
            try
            {
                lock (_lock)
                {
                    byte[] sourceChunks = MergeAllChunk(blocks);
                    byte[] chunksData = Extensions.CompressGzip(sourceChunks);
                    byte[] chunksHashed = Encoding.ASCII.GetBytes(Extensions.SHAHash(chunksData)); //add hash to ensure data is correct

                    //headers always 5 bytes, 4 bytes for data length and 1 byte for command type, add more 40 bytes for hash string
                    int numberOfChunk = (chunksData.Length + chunksHashed.Length + 8191) / 8192; // NumberPacketByTotalSIze(chunks.Length + 5); 

                    int dataSendLength = chunksData.Length + chunksHashed.Length;

                    //checksum
                    Buffer.BlockCopy(chunksHashed, 0, _dataSend, 0, chunksHashed.Length);

                    //data
                    Buffer.BlockCopy(chunksData, 0, _dataSend, chunksHashed.Length, chunksData.Length);

                    CustomScreenEventArgs chunks = new CustomScreenEventArgs(
                       type: RemoteType.Chunks,
                       totalSize: dataSendLength
                    );
                    //cut data to chunk(8192 bytes)  and send
                    for (int i = 0; i < numberOfChunk; i++)
                    {
                        int offset = i * CHUNK_SIZE;
                        int remain = dataSendLength - offset;

                        int packetSize = Math.Min(CHUNK_SIZE, remain);

                        // Note: Cannot use a shared buffer here because Send() adds the task to a queue.
                        // If a shared buffer is used, the next packet may overwrite the previous data,
                        // causing all queued packets to contain the same (last) data.
                        byte[] chunkData = new byte[packetSize];

                        //data
                        Buffer.BlockCopy(_dataSend, offset, chunkData, 0, packetSize);
                        chunks.Data.Add(chunkData);
                    }
                    ScreenEvent?.Invoke(null, chunks);
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "ScreenHook").Error(ex, "Chunks event error");
            }
        }
        // Merge all chunks into a single byte array
        private unsafe byte[] MergeAllChunk(List<ScreenBlock> blocks)
        {
            using (var ms = new MemoryStream())
            {
                int count = blocks.Count;

                for (int i = 0; i < count; i++)
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
                    ms.Write(blocks[i].Bytes, 0, blocks[i].Bytes.Length); // Write the chunk data
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
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_capture != null)
                    {
                        _capture.Dispose();
                        _capture = null;
                    }

                    _cancel.Cancel();
                    int count = 0;
                    while (_backgroundWorker.IsBusy && count++ < 20)
                        Thread.Sleep(100);

                    if (!_backgroundWorker.IsBusy)
                    {
                        _backgroundWorker.DoWork -= DoWork;
                        _backgroundWorker.Dispose();
                    }
                }

                _disposed = true;
            }
        }
    }
}
