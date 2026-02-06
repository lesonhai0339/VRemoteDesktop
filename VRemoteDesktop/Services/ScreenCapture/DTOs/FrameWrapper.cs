using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;

namespace VRemoteDesktop.Services.ScreenCapture.DTOs
{
    public class FrameWrapper
    {

        private readonly object _lock = new object();
        private IntPtr _hBitmap;
        private IntPtr _bits;
        private IntPtr _memDC;
        private Rectangle[] _writer;
        private Rectangle[] _reader;
        private int _cols;
        private int _rows;
        private int _size;
        private int _isReading;
        private int _isWriting;
        private int _readingFailCount = 0;
        private int _writingFailCount = 0;
        public FrameWrapper(int cols, int rows, int size)
        {
            _cols = cols;   
            _rows = rows;
            _size = size;
        }
        public object Lock => _lock;
        public IntPtr Image => _bits;


        public void Swap()
        {
            if (Thread.VolatileRead(ref _isReading) == 1)
            {
                long currentFails = Interlocked.Increment(ref _readingFailCount);
                if (currentFails > 10)
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
                var temp = _writer;
                _writer = _reader;
                _reader = temp;

                _readingFailCount = 0;
                _writingFailCount = 0;
            }
        }
        public Rectangle[] GetWriter()
        {
            if (Interlocked.CompareExchange(ref _isWriting, 1, 0) != 0)
                return null;

            return _writer;
        }
        public void WriteCompleted()
        {
            if(Interlocked.CompareExchange(ref _isWriting, 0, 1) == 1)
            {
                Interlocked.Exchange(ref _writingFailCount, 0);
            }
        }
        public Rectangle[] GetReader()
        {
            if (Interlocked.CompareExchange(ref _isReading, 1, 0) != 0)
                return null;

            return _reader;
        }
        public void ReadCompleted()
        {
            if (Interlocked.CompareExchange(ref _isReading, 0, 1) == 1)
            {
                Interlocked.Exchange(ref _readingFailCount, 0);
            }
        }
        public int GetRectangleIndex(Rectangle rect)
        {
            if (rect.IsEmpty)
                return -1;

            return ((rect.Y / _size) * _cols) + (rect.X / _size);
        }

    }
}
