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
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace VRemoteDesktop.Services.ScreenCapture
{
    public interface IScreenCaptureServiceListener : IDisposable
    {
        void StartCapture();
        void StopCapture();
        event EventHandler<ScreenCaptureEventArgs> ScreenEvent;
        bool IsCapturing { get; set; }
        List<byte[]> GetScreenPackets();
        List<byte[]> GetScreenPacketsWithoutChecksum();

    }
    public class ScreenCaptureService : IScreenCaptureServiceListener
    {
        private readonly object _lock = new object(); // For thread safety. Can use ReadWriteLockSlim instead
        private volatile bool _isCapturing;
        private bool _disposed = false;
        private byte[] _dataSend;

        private readonly IScreenCapture _capture;
        private BackgroundWorker _backgroundWorker;
        public event EventHandler<ScreenCaptureEventArgs> ScreenEvent;
        private CancellationTokenSource _cancel = new CancellationTokenSource();
        public ScreenCaptureService(IScreenCapture screenCapture)
        {
            IsCapturing = false;
            _dataSend = new byte[1024 * 1024];
            _capture = screenCapture;
            BackgroundWorker = new BackgroundWorker();
            BackgroundWorker.WorkerSupportsCancellation = true;
        }
        #region Properties
        public bool IsCapturing
        {
            get
            {
                lock (_lock)
                {
                    return _isCapturing;
                }
            }
            set
            {
                lock (_lock)
                {
                    _isCapturing = value;
                }
            }
        }
        public bool IsDisposed
        {
            get => _disposed;
        }
        private BackgroundWorker BackgroundWorker
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
                IsCapturing = true;
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
                IsCapturing = false;
                _cancel?.Cancel();
                BackgroundWorker.CancelAsync();
                Log.ForContext("Screen", "RemoteDesktopClient")
                                                         .Info($"Stop capture");
            }
        }
        private void DoWork(object sender, DoWorkEventArgs e)
        {
            int frameTime = 1000 / DefaultScreen.DEFAULT_FPS;
            while (!_cancel.IsCancellationRequested)
            {
                int start = Environment.TickCount;
                var screens = _capture.GetScreen();
                if (screens.Count > 0)
                {
                    //Impossible screen capture can exceed Int.MaxValue(~2GB)
                    int totalSize = 0;
                    for (int i = 0; i < screens.Count; i++)
                    {
                        totalSize = checked(totalSize + screens[i].TotalSize);
                    }

                    ScreenType screenEnum = screens.Count == 1 && screens[0].IsFullScreen 
                        ? ScreenType.FULLSCREEN 
                        : ScreenType.REGIONSCREENS;

                    switch (screenEnum)
                    {
                        case ScreenType.FULLSCREEN:
                            ScreenToPacketsWithoutChecksum(screens[0], totalSize);
                            //ScreenToPackets(screens[0], totalSize);
                            break;
                        case ScreenType.REGIONSCREENS:
                            ScreenRegionsChangedToPacketsWithoutChecksum(screens, totalSize);
                            //ScreenRegionsChangedToPackets(screens, totalSize);
                            break;
                    }
                }
                int elapsed = unchecked(Environment.TickCount - start); //ensure result always correct (it goes negative every 24 days)
                int remainTime = frameTime - elapsed;
                if (remainTime > 0)
                {
                    Thread.Sleep(remainTime);
                }
            }
        }
        public List<byte[]> GetScreenPacketsWithoutChecksum()
        {
            try
            {
                var screens = _capture.GetScreen();
                if (screens[0].Bytes == null || screens[0].Bytes.Length == 0)
                {
                    return null;
                }
                var result  = ByteArrayHelper.CompressGZip(screens[0].Bytes);
                if (!result.IsSuccess)
                    return null;

                byte[] screenCaptureCompressed = result.Data;
                int dataSendLength = checked(screenCaptureCompressed.Length);
                if (_dataSend.Length < dataSendLength)
                {
                    _dataSend = new byte[dataSendLength];
                }
                lock (_lock)
                {
                    int offset = 0;

                    Buffer.BlockCopy(screenCaptureCompressed, 0, _dataSend, offset, screenCaptureCompressed.Length);
                    offset += screenCaptureCompressed.Length;

                    var listByteArray = ByteArrayHelper.ToListByteArray(_dataSend, offset, DefaultScreen.DEFAULT_CHUNK_SIZE).GetResult();
                    return listByteArray;
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "ScreenHook").Error(ex, "Screen event error");
            }
            return null;
        }
        private void ScreenToPacketsWithoutChecksum(ScreenRegion screen, int totalChunksSize)
        {
            if (screen == null || totalChunksSize < 0)
            {
                Log.ForContext("", this.GetType().Name).Error("ScreenToPackets Arguments are null or empty");
                return;
            }
            if (screen.Bytes == null || screen.Bytes.Length == 0)
            {
                Log.ForContext("", this.GetType().Name).Error("ScreenToPackets error: data is null or empty");
                return;
            }
            if (screen.Rectangle == null)
            {
                Log.ForContext("", this.GetType().Name).Error("ScreenToPackets error: rectangle is null or empty");
                return;
            }

            try
            {
                var result = ByteArrayHelper.CompressGZip(screen.Bytes);
                if (!result.IsSuccess)
                    return;

                byte[] screenCaptureCompressed = result.Data;
                int dataLength = checked(screenCaptureCompressed.Length);
                if (_dataSend.Length < dataLength)
                {
                    _dataSend = new byte[dataLength];
                }
                lock (_lock)
                {

                    int offset = 0;

                    Buffer.BlockCopy(screenCaptureCompressed, 0, _dataSend, offset, screenCaptureCompressed.Length);
                    offset += screenCaptureCompressed.Length;

                    ScreenCaptureEventArgs screenArgs = new ScreenCaptureEventArgs(
                        type: SocketDataType.Screen,
                        totalSize: dataLength
                    );

                    var byteArrayToListByteArray = ByteArrayHelper.ToListByteArray(_dataSend, dataLength, DefaultScreen.DEFAULT_CHUNK_SIZE).GetResult();
                    screenArgs.Data = byteArrayToListByteArray;
                    ScreenEvent?.Invoke(this, screenArgs);
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "ScreenHook").Error(ex, "Screen event error");
            }
        }
        private void ScreenRegionsChangedToPacketsWithoutChecksum(List<ScreenRegion> regions, int totalChunksSize)
        {
            if (regions == null || totalChunksSize < 0)
            {
                Log.ForContext("", this.GetType().Name).Error("ScreenRegionsChangedToPackets Arguments are null or empty");
                return;
            }
            if (regions.Count == 0)
            {
                Log.ForContext("", this.GetType().Name).Error("ScreenRegionsChangedToPackets error: regions is null or empty");
                return;
            }
            try
            {
                byte[] mergedChangedRegions = ConvertChangedRegionsToByteArray(regions);
                if (mergedChangedRegions.Length == 0)
                    return;

                var result = ByteArrayHelper.CompressGZip(mergedChangedRegions);
                if (!result.IsSuccess)
                    return;

                byte[] changedRegionsCompressed = result.Data;

                int dataSendLength = checked(changedRegionsCompressed.Length);

                if (_dataSend.Length < dataSendLength)
                {
                    _dataSend = new byte[dataSendLength];
                }
                lock (_lock)
                {
                    int offset = 0;

                    Buffer.BlockCopy(changedRegionsCompressed, 0, _dataSend, offset, changedRegionsCompressed.Length);
                    offset += changedRegionsCompressed.Length;

                    ScreenCaptureEventArgs chunksArgs = new ScreenCaptureEventArgs(
                       type: SocketDataType.Chunks,
                       totalSize: dataSendLength
                    );
                    var byteArrayToListByteArray = ByteArrayHelper.ToListByteArray(_dataSend, dataSendLength, DefaultScreen.DEFAULT_CHUNK_SIZE).GetResult();
                    chunksArgs.Data = byteArrayToListByteArray;
                    ScreenEvent?.Invoke(this, chunksArgs);
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "ScreenHook").Error(ex, "Chunks event error");
            }
        }
        public List<byte[]> GetScreenPackets()
        {
            try
            {
                var screens = _capture.GetScreen();
                if (screens[0].Bytes == null || screens[0].Bytes.Length == 0)
                {
                    return null;
                }
                byte[] screenCaptureCompressed = ByteArrayHelper.CompressGZip(screens[0].Bytes).GetResult();
                byte[] checksum = Encoding.ASCII.GetBytes(StringHelper.SHAHash(screenCaptureCompressed));
                int dataSendLength = checked(screenCaptureCompressed.Length + checksum.Length);
                if (_dataSend.Length < dataSendLength)
                {
                    _dataSend = new byte[dataSendLength];
                }
                lock (_lock)
                { 
                    int offset = 0; 
                    
                    Buffer.BlockCopy(checksum, 0, _dataSend, offset, checksum.Length); 
                    offset += checksum.Length; 

                    Buffer.BlockCopy(screenCaptureCompressed, 0, _dataSend, offset, screenCaptureCompressed.Length); 
                    offset += screenCaptureCompressed.Length; 

                    var listByteArray = ByteArrayHelper.ToListByteArray(_dataSend, offset, DefaultScreen.DEFAULT_CHUNK_SIZE).GetResult(); 
                    return listByteArray; 
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "ScreenHook").Error(ex, "Screen event error");
            }
            return null;
        }
        // Send full screen to sender at first connect
        private void ScreenToPackets(ScreenRegion screen, int totalChunksSize)
        {
            if(screen == null || totalChunksSize < 0)
            {
                Log.ForContext("", this.GetType().Name).Error("ScreenToPackets Arguments are null or empty");
                return;
            }
            if (screen.Bytes == null || screen.Bytes.Length == 0)
            {
                Log.ForContext("", this.GetType().Name).Error("ScreenToPackets error: data is null or empty");
                return;
            }
            if (screen.Rectangle == null)
            {
                Log.ForContext("", this.GetType().Name).Error("ScreenToPackets error: rectangle is null or empty");
                return;
            }

            try
            {
                byte[] screenCaptureCompressed = ByteArrayHelper.CompressGZip(screen.Bytes).GetResult();
                byte[] checksum = Encoding.ASCII.GetBytes(StringHelper.SHAHash(screenCaptureCompressed));
                int dataLength = checked(screenCaptureCompressed.Length + checksum.Length);
                if (_dataSend.Length < dataLength)
                {
                    _dataSend = new byte[dataLength];
                }
                lock (_lock)
                {

                    int offset = 0;

                    Buffer.BlockCopy(checksum, 0, _dataSend, offset, checksum.Length);
                    offset += checksum.Length;

                    Buffer.BlockCopy(screenCaptureCompressed, 0, _dataSend, offset, screenCaptureCompressed.Length);
                    offset += screenCaptureCompressed.Length;

                    ScreenCaptureEventArgs screenArgs = new ScreenCaptureEventArgs(
                        type: SocketDataType.Screen,
                        totalSize: dataLength
                    );

                    var byteArrayToListByteArray = ByteArrayHelper.ToListByteArray(_dataSend, dataLength, DefaultScreen.DEFAULT_CHUNK_SIZE).GetResult();
                    screenArgs.Data = byteArrayToListByteArray;
                    ScreenEvent?.Invoke(this, screenArgs);
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "ScreenHook").Error(ex, "Screen event error");
            }
        }
        //Capture and send screen region change to sender
        private void ScreenRegionsChangedToPackets(List<ScreenRegion> regions, int totalChunksSize)
        {
            if (regions == null || totalChunksSize < 0)
            {
                Log.ForContext("", this.GetType().Name).Error("ScreenRegionsChangedToPackets Arguments are null or empty");
                return;
            }
            if (regions.Count == 0 )
            {
                Log.ForContext("", this.GetType().Name).Error("ScreenRegionsChangedToPackets error: regions is null or empty");
                return;
            }
            try
            {
                byte[] mergedChangedRegions = ConvertChangedRegionsToByteArray(regions);
                if (mergedChangedRegions.Length == 0)
                    return;

                byte[] changedRegionsCompressed = ByteArrayHelper.CompressGZip(mergedChangedRegions).GetResult();

                byte[] checksum = Encoding.ASCII.GetBytes(StringHelper.SHAHash(changedRegionsCompressed)); //add hash to ensure data is correct

                int dataSendLength = changedRegionsCompressed.Length + checksum.Length;

                if (_dataSend.Length < dataSendLength)
                {
                    _dataSend = new byte[dataSendLength];
                }
                lock (_lock)
                {
                    int offset = 0;

                    Buffer.BlockCopy(checksum, 0, _dataSend, offset, checksum.Length);
                    offset += checksum.Length ;

                    Buffer.BlockCopy(changedRegionsCompressed, 0, _dataSend, offset, changedRegionsCompressed.Length);
                    offset += changedRegionsCompressed.Length;

                    ScreenCaptureEventArgs chunksArgs = new ScreenCaptureEventArgs(
                       type: SocketDataType.Chunks,
                       totalSize: dataSendLength
                    );
                    var byteArrayToListByteArray = ByteArrayHelper.ToListByteArray(_dataSend, dataSendLength, DefaultScreen.DEFAULT_CHUNK_SIZE).GetResult();
                    chunksArgs.Data = byteArrayToListByteArray;
                    ScreenEvent?.Invoke(this, chunksArgs);
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "ScreenHook").Error(ex, "Chunks event error");
            }
        }
        //private unsafe byte[] ConvertChangedRegionsToByteArray(List<ScreenRegion> regions)
        private byte[] ConvertChangedRegionsToByteArray(List<ScreenRegion> regions)
        {
            using (var ms = new MemoryStream())
            {
                int count = regions.Count;

                for (int i = 0; i < count; i++)
                {
                    var region = regions[i];

                    if (region == null || region.Bytes == null || region.Bytes.Length == 0 || region.Rectangle == null)
                        continue;
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
                    ms.Write(BitConverter.GetBytes(region.Bytes.Length), 0, ByteConstants.INT32_LENGTH);
                    ms.Write(BitConverter.GetBytes(region.Rectangle.X), 0, ByteConstants.INT32_LENGTH);
                    ms.Write(BitConverter.GetBytes(region.Rectangle.Y), 0, ByteConstants.INT32_LENGTH);
                    ms.Write(BitConverter.GetBytes(region.Rectangle.Width), 0, ByteConstants.INT32_LENGTH);
                    ms.Write(BitConverter.GetBytes(region.Rectangle.Height), 0, ByteConstants.INT32_LENGTH);
                    ms.Write(region.Bytes, 0, region.Bytes.Length);
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
            if (disposing)
            {
                if (_disposed) return;

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
                _dataSend = null;
                _cancel.Dispose();
                _disposed = true;
            }
        }
    }
}
