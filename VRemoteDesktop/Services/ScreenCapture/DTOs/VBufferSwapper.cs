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
        public void Free()
        {
            Interlocked.Exchange(ref _isReading, 0);
        }
    }
}
