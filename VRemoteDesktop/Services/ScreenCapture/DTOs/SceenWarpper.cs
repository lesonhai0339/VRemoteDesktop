using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace VRemoteDesktop.Services.ScreenCapture.DTOs
{
    public class RawBitmap
    {
        private int _lock;
        private IntPtr Bitmap;
        public RawBitmap(IntPtr bitmap)
        {
            Bitmap = bitmap;
            _lock = 0;
        }
        public void Lock()
        {
            Interlocked.Exchange(ref _lock, 1);
        }
        public void Free()
        {
            Interlocked.Exchange(ref _lock, 0);
        }
    }
    public class SceenWarpper
    {

        // 
    }
}
