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
        private int _isReading = 0; // 0: rảnh, 1: đang ghi
        private int _isWriting = 0; // 0: rảnh, 1: đang ghi
        private int _isDataFill = 0;
        public VBufferSwapper(ImageSwapper writer, ImageSwapper reader)
        {
            array[_writerIdx] = writer;
            array[_readerIdx] = reader;
        }
        public ImageSwapper BeginWrite()
        {
            Interlocked.Exchange(ref _isWriting, 1);
            return array[_writerIdx];
        }
        public void EndWrite()
        {
            Interlocked.Exchange(ref _isWriting, 0);
        }
        public ImageSwapper GetDataBuffer()
        {
            return array[_writerIdx];
            if (Interlocked.CompareExchange(ref _isReading, 1, 0) != 0)
                return null;

            if (Interlocked.CompareExchange(ref _isWriting, 1, 1) == 1)
            {
                Interlocked.Exchange(ref _isReading, 0);
                return null;
            }

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
    //    private readonly object _lock = new object();
    //    private ImageSwapper[] array = new ImageSwapper[2];
    //    private int _writerIdx = 0;
    //    private int _readerIdx = 1;

    //    // Sử dụng 1 flag duy nhất để quản lý trạng thái bận
    //    private int _isBusy = 0;

    //    public VBufferSwapper(ImageSwapper writer, ImageSwapper reader)
    //    {
    //        array[0] = writer;
    //        array[1] = reader;
    //    }

    //    // Luồng Ghi gọi cái này trước khi bắt đầu ghi
    //    public ImageSwapper BeginWrite()
    //    {
    //        // Đánh dấu là đang ghi, ngăn không cho Swap
    //        Interlocked.Exchange(ref _isBusy, 1);
    //        return array[_writerIdx];
    //    }

    //    // Luồng Ghi gọi cái này sau khi ghi xong
    //    public void EndWrite()
    //    {
    //        Interlocked.Exchange(ref _isBusy, 0);
    //    }
    //    public ImageSwapper GetWriteBuffer()
    //    {
    //        return array[_writerIdx];
    //    }

    //    public ImageSwapper GetDataBuffer()
    //    {
    //        // Nếu đang bận Ghi hoặc đang có luồng khác Đọc, bỏ qua để tránh lỗi
    //        if (Interlocked.CompareExchange(ref _isBusy, 1, 0) != 0)
    //            return null;

    //        lock (_lock)
    //        {
    //            // Hoán đổi an toàn vì đã chiếm quyền Busy
    //            var temp = _readerIdx;
    //            _readerIdx = _writerIdx;
    //            _writerIdx = temp;

    //            // Trả về buffer mới để đọc, nhưng vẫn giữ _isBusy = 1 cho đến khi Free()
    //            return array[_readerIdx];
    //        }
    //    }

    //    public void Free()
    //    {
    //        Interlocked.Exchange(ref _isBusy, 0);
    //    }
    //}
}
