using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using Vsign4.VRemoteDesktop.Services.ScreenCapture.DTOs;
using Vsign4.VRemoteDesktop.Services.ScreenCapture.Enums;
using Vsign4.VRemoteDesktop.Services.SessionManagement.DTOs;
using Vsign4.VRemoteDesktop.Utils;

namespace Vsign4.VRemoteDesktop.Services.ScreenCapture.GDI
{
    public class VRegions : VScreen
    {
        private const int INFLIGHT_LIMIT = 10;  //accept maximum 10 inflight packet
        private const long DELAY_TIME = 50; //50ms

        private readonly object _lock = new object();
        private readonly int _width;
        private readonly int _height;
        private readonly int _bytePerPixel;

        private readonly Interop.CaptureApi.BITMAPINFO _bitmapInfo;
        //Writer buffer
        private readonly ImageSwapper _writer;
        //Reader buffer
        private readonly ImageSwapper _reader;
        //Swapper contain writer, reader
        private readonly VBufferSwapper _bufferSwapper;

        private readonly int _totalColumns; // Number of columns divide to regionSize
        private readonly int _totalRows;  // Number of rows divide to regionSize
        private readonly int _regionSize; // Region size(Rectangle(_regionSize, _regionSize))
        private readonly Rectangle[] _dirtyRegions; // Fixed array with formula Rectangle[] = totalCols * totalRows
                                                    // Chia man hinh thanh 1 luoi grid voi kich thuoc co dinh theo regionSize, moi region co 1 index co dinh trong array.
                                                    // Khi co 2 dirty region trung nhau thi no se ghi de len thay vi them. Dung co che nay de dam bao tranh viec copy du lieu
                                                    // tren cung 1 rectangle ma van dam bao khong bo sot.

        private int _disposed = 0;
        private int _inflight = 0;
        private int _hasData = 0; // 0: empty, 1: has
        private int _fullScreenCompleted = 0; //0: uncompleted, 1: completed
        private long _lastSendTimestamp = Stopwatch.GetTimestamp();
        private bool _busy = false;

        public VRegions(int width, int height, int bytePerPixel, int regionSize = 16)
            : base(width, height, bytePerPixel, regionSize)
        {
            _width = width;
            _height = height;
            _bytePerPixel = bytePerPixel;
            _regionSize = regionSize;

            _bitmapInfo = base.InitBitmapInfo(_width, _height, (ushort)(_bytePerPixel * 8), 0);

            _writer = new ImageSwapper(_width, _height, _regionSize);
            _reader = new ImageSwapper(_width, _height, _regionSize);
            base.InitCaptureBuffer(ref _writer.HBitmap, ref _writer.MemDC, ref _writer.Bits, IntPtr.Zero, 0, IntPtr.Zero, _bitmapInfo);
            base.InitCaptureBuffer(ref _reader.HBitmap, ref _reader.MemDC, ref _reader.Bits, IntPtr.Zero, 0, IntPtr.Zero, _bitmapInfo);
            _bufferSwapper = new VBufferSwapper(_writer, _reader);

            _totalColumns = (_width + (_regionSize - 1)) / _regionSize;
            _totalRows = (_height + (_regionSize - 1)) / _regionSize;
            _regionSize = regionSize;

            _dirtyRegions = new Rectangle[_totalColumns * _totalRows];
        }
        #region Properties
        public object Lock 
        {
            get { 
                return _lock;
            } 
        }
        public bool FullScreenCompleted
        {
            get
            {
                return Thread.VolatileRead(ref _fullScreenCompleted) == 1;
            }
        }
        public bool HasData
        {
            get
            {
               return Thread.VolatileRead(ref _hasData) == 1;
            }
        }
        public VBufferSwapper BufferSwapper
        {
            get
            {
                return _bufferSwapper;
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Full screen had been sent successfully
        /// </summary>
        public void SetFullScreenCompleted()
        {
            Interlocked.Exchange(ref _fullScreenCompleted, 1);
        }
        /// <summary>
        /// Notify that have data to send
        /// </summary>
        public void SetHasData()
        {
            Interlocked.Exchange(ref _hasData, 1);
        }
        /// <summary>
        ///  <para>Using to calculate index of current rectangle on fixed array(each rectangle have a  fixed index)</para>
        ///  <para>Now use in <see cref="ImageSwapper.Add(RegionFrame)"/></para>
        /// </summary>
        /// <param name="rect"></param>
        /// <param name="regionSize"></param>
        /// <returns></returns>
        [Obsolete("Not use")]
        public int GetRectangleIndex(Rectangle rect, int regionSize = 0)
        {
            if (rect.IsEmpty)
                return -1;

            if (regionSize <= 0)
                regionSize = _regionSize;

            return ((rect.Y / regionSize) * _totalColumns) + (rect.X / regionSize);
        }
        /// <summary>
        /// Notify that can be send data
        /// </summary>
        /// <returns></returns>
        public bool ReadyToSend()
        {
            // Check fullScreen completed, if not break;
            if (Thread.VolatileRead(ref _fullScreenCompleted) != 1)
                return false;

            var now = Stopwatch.GetTimestamp();
            var elapsedMs = (now - _lastSendTimestamp) * 1000 / Stopwatch.Frequency;
            if (elapsedMs < DELAY_TIME)
                return false;

            lock (_lock)
            {

                if (_inflight < INFLIGHT_LIMIT)
                {
                    _lastSendTimestamp = now;
                    _inflight++;
                    return true;
                }
                else
                {
                    if (elapsedMs > 3 * 1000)
                    {
                        //reset if exceed timeout
                        _lastSendTimestamp = now;
                        _inflight--;
                        return true;
                    }
                    return false;
                }
            }
        }
        /// <summary>
        /// Notify that sending completed
        /// </summary>
        public void SendCompleted()
        {
            lock (_lock)
            {
                if (_inflight > 0)
                {
                    _inflight--;
                }
            }
        }
        public ScreenDataDto GetData(VScreenType type)
        {
            //Dùng để chuyển đổi vị trí giữa writer và reader
            //Buffer swapper chứa 2 ImageSwapper (writer, reader)   
            //Khi không đang ghi hoặc đang đọc(có 2 biến isReading và isWriting trong bufferswapper), ImageSwapper(writer) sẽ chuyển sang ImageSwapper(reader) để đọc dữ liệu  
            //ImageSwapper(reader) sẽ trở thành ImageSwapper(writer) để ghi dữ liệu mới vào
            //Mục đích nhằm đảm bảo tính toàn vẹn dữ liệu và không mất dữ liệu khi ghi và đọc song song
            _bufferSwapper.Swap();
            try
            {
                var reader = _bufferSwapper.BeginRead();
                if (reader == null)
                    return null;

                Rectangle[] regionsToProcess = null;
                Rectangle fullScreenToProcess = Rectangle.Empty;

                lock (reader.Lock)
                {
                    if (type == VScreenType.FullScreen)
                    {
                        fullScreenToProcess = reader.FullScreen;
                    }
                    else
                    {
                        regionsToProcess = reader.ChangedRegions;
                    }
                }
                if (fullScreenToProcess.IsEmpty && (regionsToProcess == null || regionsToProcess.Length == 0))
                    return null;

                int dirtyRegionsCount = 0;
                lock (_lock)
                {
                    dirtyRegionsCount = (type == VScreenType.FullScreen)
                                        ? GetDirtyRegions(fullScreenToProcess)
                                        : GetDirtyRegions(regionsToProcess);
                }

                if (dirtyRegionsCount == 0)
                {
                    _bufferSwapper.EndRead();
                    return null;
                }

                int rentLength = GetScreenDataLength(_dirtyRegions, dirtyRegionsCount, _bytePerPixel);
                byte[] buffer = VArrayPool.Rent(rentLength);
                int offset = 0;
                for (int i = 0; i < dirtyRegionsCount; i++)
                {
                    base.GetRegionsData(
                        ref offset,
                        buffer,
                        reader.Bits,//data,
                        _width,
                        _dirtyRegions[i].X,
                        _dirtyRegions[i].Y,
                        _dirtyRegions[i].Width,
                        _dirtyRegions[i].Height,
                        _bytePerPixel);
                }
                return new ScreenDataDto(buffer, 0, offset + 1);
            }
            catch (Exception ex)
            {
                SendCompleted();
                return null;
            }
            finally
            {
                //_bufferSwapper.Free();
                Interlocked.Exchange(ref _hasData, 0);
            }
        }
        private int GetDirtyRegions(Rectangle[] source)
        {
            int count = 0;
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i].Width > 0 && source[i].Height > 0)
                {
                    _dirtyRegions[count] = source[i];
                    count++;
                }
            }
            return count;
        }
        private int GetDirtyRegions(Rectangle source)
        {
            int count = 0;
            if (source.Width > 0 && source.Height > 0)
            {
                _dirtyRegions[count] = source;
                count++;
            }
            return count;
        }
        public void ReadCompleted()
        {

            var reader = _bufferSwapper.GetRead();
            if (reader == null)
                return;
            _bufferSwapper.EndRead();
        }
        #endregion
        public override void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;
            try
            {
                if (disposing)
                {
                    Array.Clear(_dirtyRegions, 0, _dirtyRegions.Length);

                    if (_writer != null)
                        _writer.Free();

                    if (_reader != null)
                        _reader.Free();

                    if (_bufferSwapper != null)
                        _bufferSwapper.Dispose();
                }
            }
            catch { }
        }
    }
}
