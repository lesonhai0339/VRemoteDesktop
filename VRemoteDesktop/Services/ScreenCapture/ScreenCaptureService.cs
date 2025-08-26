using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Models;
using VRemoteDesktop.Utils;
using static VRemoteDesktop.Utils.Logger;
using static VRemoteDesktop.Utils.DefaultValue;

namespace VRemoteDesktop.Services.ScreenCapture
{
    public interface IScreenCaptureServiceListener : IDisposable
    {
        void StartCapture();
        void StopCapture();
        event EventHandler<ScreenCaptureEventArgs> ScreenEvent;
        bool IsCapturing { get; set; }
    }
    public class ScreenCaptureService : IScreenCaptureServiceListener
    {
        private readonly object _lock = new object(); // For thread safety. Can use ReadWriteLockSlim instead
        private readonly int CHUNK_SIZE = DEFAULT_CHUNK_SIZE;
        private readonly int FPS = DEFAULT_FPS;
        private volatile bool _isCapturing;
        private bool _disposed = false;
        private byte[] _buffer = new byte[20];
        private byte[] _dataSend;

        private IScreenCapture _capture;
        private ScreenCaptureConfig _config;
        private BackgroundWorker _backgroundWorker;
        public event EventHandler<ScreenCaptureEventArgs> ScreenEvent;
        private CancellationTokenSource _cancel = new CancellationTokenSource();
        public ScreenCaptureService(ScreenCaptureConfig config, ScreenCapture screenCapture)
        {
            InitializeCapture(config, screenCapture);
        }
        private void InitializeCapture(ScreenCaptureConfig config, ScreenCapture screenCapture)
        {
            IsCapturing = false;
            _dataSend = new byte[1024 * 1024];
            _config = config ?? new ScreenCaptureConfig();
            _capture = screenCapture ?? new ScreenCapture();
            BackgroundWorker = new BackgroundWorker();
            BackgroundWorker.WorkerSupportsCancellation = true;
        }
        #region Properties
        public bool IsCapturing
        {
            get => _isCapturing;
            set => _isCapturing = value;
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
            Stopwatch stopwatch = new Stopwatch();
            while (!_cancel.IsCancellationRequested)
            {
                stopwatch.Restart();
                var screens = _capture.GetScreen();
                if (screens.Any())
                {
                    long totalSize = checked(screens.Sum(x => x.TotalSize));
                    ScreenType screenEnum = screens.Count == 1 && screens[0].IsFullScreen ? ScreenType.FULLSCREEN : ScreenType.REGIONSCREENS;
                    switch (screenEnum)
                    {
                        case ScreenType.FULLSCREEN:
                            ScreenToChunks(screens[0], totalSize);
                            break;
                        case ScreenType.REGIONSCREENS:
                            RegionsChangedToChunks(screens, totalSize);
                            break;
                    }
                }
                stopwatch.Stop();
                int elapsed = (int)stopwatch.ElapsedMilliseconds;
                int frameTime = 1000 / FPS;
                int remainTime = frameTime - elapsed;
                if (remainTime > 0)
                {
                    Thread.Sleep(remainTime);
                }
            }
        }
        // Send full screen to sender at first connect
        private void ScreenToChunks(ScreenRegion screen, long totalChunksSize)
        {
            try
            {
                if (_dataSend.Length < totalChunksSize + 40)
                {
                    _dataSend = new byte[totalChunksSize + 40];
                }
                lock (_lock)
                {
                    byte[] screenCaptureCompressed = ByteArrayHelper.CompressGzip(screen.Bytes).GetResult();
                    byte[] checksum = Encoding.ASCII.GetBytes(StringHelper.SHAHash(screenCaptureCompressed));
                    int dataLength = screenCaptureCompressed.Length + checksum.Length;

                    var byteCombined = ByteArrayHelper.Combine(checksum, screenCaptureCompressed).GetResult();
                    _dataSend = byteCombined;
                    ScreenCaptureEventArgs screenArgs = new ScreenCaptureEventArgs(
                        type: DataType.Screen,
                        totalSize: dataLength
                    );

                    var byteArrayToListByteArray = ByteArrayHelper.ToListByteArray(_dataSend, dataLength, CHUNK_SIZE).GetResult();
                    screenArgs.Data = byteArrayToListByteArray;
                    ScreenEvent?.Invoke(null, screenArgs);
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "ScreenHook").Error(ex, "Screen event error");
            }
        }
        //Capture and send screen region change to sender
        private void RegionsChangedToChunks(List<ScreenRegion> regions, long totalChunksSize)
        {
            try
            {
                if (_dataSend.Length < totalChunksSize + 40)
                {
                    _dataSend = new byte[totalChunksSize + 40];
                }
                lock (_lock)
                {
                    byte[] mergedChangedRegions = ConvertChangedRegionsToByteArray(regions);
                    byte[] changedRegionsCompressed = ByteArrayHelper.CompressGzip(mergedChangedRegions).GetResult();
                    byte[] checksum = Encoding.ASCII.GetBytes(StringHelper.SHAHash(changedRegionsCompressed)); //add hash to ensure data is correct

                    int dataSendLength = changedRegionsCompressed.Length + checksum.Length;

                    var byteCombined = ByteArrayHelper.Combine(checksum, changedRegionsCompressed).GetResult(); ;
                    _dataSend = byteCombined;


                    ScreenCaptureEventArgs chunksArgs = new ScreenCaptureEventArgs(
                       type: DataType.Chunks,
                       totalSize: dataSendLength
                    );

                    var byteArrayToListByteArray = ByteArrayHelper.ToListByteArray(_dataSend, dataSendLength, CHUNK_SIZE).GetResult();
                    chunksArgs.Data = byteArrayToListByteArray;
                    ScreenEvent?.Invoke(null, chunksArgs);
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "ScreenHook").Error(ex, "Chunks event error");
            }
        }
        //private unsafe byte[] ConverChangedRegionsToByteArray(List<ScreenRegion> regions)
        private byte[] ConvertChangedRegionsToByteArray(List<ScreenRegion> regions)
        {
            using (var ms = new MemoryStream())
            {
                int count = regions.Count;

                for (int i = 0; i < count; i++)
                {

                    //fixed (byte* p = _buffer)
                    //{
                    //    int* pInt = (int*)p;
                    //    pInt[0] = regions[i].Bytes.Length; // Length of the chunk
                    //    pInt[1] = regions[i].Rectangle.X; // X coordinate of the rectangle
                    //    pInt[2] = regions[i].Rectangle.Y; // Y coordinate of the rectangle
                    //    pInt[3] = regions[i].Rectangle.Width; // Width of the rectangle
                    //    pInt[4] = regions[i].Rectangle.Height; // Height of the rectangle

                    //    //note: can write like this *(pInt + 1) = blocks[i].Rectangle.X; 
                    //}
                    //ms.Write(_buffer, 0, _buffer.Length); // Write the header
                    //ms.Write(regions[i].Bytes, 0, regions[i].Bytes.Length); // Write the chunk data
                    ms.Write(BitConverter.GetBytes(regions[i].Bytes.Length), 0, 4);
                    ms.Write(BitConverter.GetBytes(regions[i].Rectangle.X), 0, 4);
                    ms.Write(BitConverter.GetBytes(regions[i].Rectangle.Y), 0, 4);
                    ms.Write(BitConverter.GetBytes(regions[i].Rectangle.Width), 0, 4);
                    ms.Write(BitConverter.GetBytes(regions[i].Rectangle.Height), 0, 4);
                    ms.Write(regions[i].Bytes, 0, regions[i].Bytes.Length);
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
                    }

                    StopCapture();
                    int count = 0;
                    while (_backgroundWorker.IsBusy && count++ < 20)
                        Thread.Sleep(100);

                    if (!_backgroundWorker.IsBusy)
                    {
                        _backgroundWorker.DoWork -= DoWork;
                        _backgroundWorker.Dispose();
                    }
                }
                ScreenEvent = null;
                _disposed = true;
            }
        }
    }
}
