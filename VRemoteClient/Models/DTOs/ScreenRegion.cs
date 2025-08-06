using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace VRemoteClient.Models.Entities
{
    public class ScreenRegion : IDisposable
    {
        public bool IsFullScreen { get; set; } // Indicates if this cell is a full-screen capture
        public Rectangle Rectangle { get; set; }
        public byte[] Bytes { get; set; }
        public int TotalSize => Bytes?.Length ?? 0; // Total size of the captured bytes

        public void Dispose()
        {
            Bytes = null;
            GC.SuppressFinalize(this);
        }
    }
}
