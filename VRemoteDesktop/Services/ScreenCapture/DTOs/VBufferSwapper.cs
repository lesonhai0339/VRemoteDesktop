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

        private int _readingFailCount = 0;
        private int _writingFailCount = 0;
        public VBufferSwapper(ImageSwapper writer, ImageSwapper reader)
        {
            array[_writerIdx] = writer;
            array[_readerIdx] = reader;
        }
        public ImageSwapper BeginWrite()
        {
            if (Interlocked.CompareExchange(ref _isWriting, 1, 0) != 0)
                return null;
            return array[_writerIdx];
        }
        public void EndWrite()
        {
            if (Interlocked.CompareExchange(ref _isWriting, 0, 1) == 1)
            {
                Interlocked.Exchange(ref _writingFailCount, 0);
            }
        }
        public ImageSwapper BeginRead()
        {
            if (Interlocked.CompareExchange(ref _isReading, 1, 0) != 0)
                return null;
            return array[_readerIdx];
        }
        public ImageSwapper GetRead()
        {
            if (Thread.VolatileRead(ref _isReading) == 1)
            {
                return array[_readerIdx];
            }
            return null;
        }
        public void EndRead()
        {
            if (Interlocked.CompareExchange(ref _isReading, 0, 1) == 1)
            {
                Interlocked.Exchange(ref _readingFailCount, 0);
            }
        }
        public void Swap()
        {
            if (Thread.VolatileRead(ref _isReading) == 1)
            {
                long currentFails = Interlocked.Increment(ref _readingFailCount);
                if(currentFails > 10)
                {
                    Interlocked.Exchange(ref _readingFailCount, 0);
                    Interlocked.Exchange(ref _isReading, 0);
                }
                return;
            }

            if (Thread.VolatileRead(ref _isReading) == 1)
            {
                long currentFails = Interlocked.Increment(ref _writingFailCount);
                if (currentFails > 10)
                {
                    Interlocked.Exchange(ref _writingFailCount, 0);
                    Interlocked.Exchange(ref _isWriting, 0);
                }
                return;
            }

            lock (_lock)
            {
                var temp = _readerIdx;
                _readerIdx = _writerIdx;
                _writerIdx = temp;
            }
        }

        internal void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
