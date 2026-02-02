using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace VRemoteDesktop.Services.ScreenCapture.DTOs
{
    public class VBufferSwapper
    {
        private object _lock = new object();
        private ImageSwapper[] array = new ImageSwapper[2];
        private int _writerIdx = 0;
        private int _readerIdx = 1;
        private int _isReading = 0;
        private int _isDataFill = 0;
        public VBufferSwapper(ImageSwapper writer, ImageSwapper reader)
        {
            array[_writerIdx] = writer;
            array[_readerIdx] = reader;
        }
        public ImageSwapper GetWriteBuffer()
        {
            return array[_writerIdx];
        }
        public ImageSwapper GetDataBuffer()
        {
            if (Interlocked.CompareExchange(ref _isReading, 1, 0) != 0)
                return null;

            lock (_lock)
            {
                var temp = _readerIdx;
                _readerIdx = _writerIdx;
                _writerIdx = temp;
                return array[_readerIdx];
            }
        }
        /// <summary>
        /// Only using for full screen capture, do not use anywhere else.
        /// Item 1 is writer, item 2 is reader
        /// </summary>
        public WriterReaderSwapper GetWriteAndReader()
        {
            if (Interlocked.CompareExchange(ref _isDataFill, 1, 0) != 0)
                return default;

            return new WriterReaderSwapper(writer: array[_writerIdx], reader: array[_readerIdx]);
        }
        public void Free()
        {
            Interlocked.Exchange(ref _isReading, 0);
        }
    }
    //public class VBufferSwapper
    //{
    //    private object _lock = new object();
    //    private IntPtr[] array = new IntPtr[2];
    //    private int _writerIdx = 0;
    //    private int _readerIdx = 1;
    //    private int _isReading = 0;
    //    private int _isDataFill = 0;
    //    public VBufferSwapper(IntPtr writer, IntPtr reader)
    //    {
    //        array[_writerIdx] = writer;
    //        array[_readerIdx] = reader;
    //    }
    //    public IntPtr GetWriteBuffer()
    //    {
    //        return array[_writerIdx];   
    //    }
    //    public IntPtr GetDataBuffer()
    //    {
    //        if (Interlocked.CompareExchange(ref _isReading, 1, 0) != 0)
    //            return IntPtr.Zero;

    //        lock (_lock)
    //        {
    //            var temp = _readerIdx;
    //            _readerIdx = _writerIdx;
    //            _writerIdx = temp;
    //            return array[_readerIdx];
    //        }
    //    }
    //    /// <summary>
    //    /// Only using for full screen capture, do not use anywhere else.
    //    /// Item 1 is writer, item 2 is reader
    //    /// </summary>
    //    public WriterReaderSwapper GetWriteAndReader()
    //    {
    //        if (Interlocked.CompareExchange(ref _isDataFill, 1, 0) != 0)
    //            return default;

    //        return new WriterReaderSwapper(writer: array[_writerIdx], reader: array[_readerIdx]);
    //    }
    //    public void Free()
    //    {
    //        Interlocked.Exchange(ref _isReading, 0);
    //    }
    //}
}
