using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Services.ScreenCapture.DTOs
{
    public readonly struct WriterReaderSwapper
    {
        public WriterReaderSwapper(ImageSwapper writer, ImageSwapper reader)
        {
            Writer = writer;
            Reader = reader;
        }
        public readonly ImageSwapper Writer;
        public readonly ImageSwapper Reader;

        public bool IsImageEmpty
        {
            get { return Writer.Bits == IntPtr.Zero || Reader.Bits == IntPtr.Zero; }
        }
    }
    //public readonly struct WriterReaderSwapper
    //{
    //    public WriterReaderSwapper(IntPtr writer, IntPtr reader)
    //    {
    //        Writer = writer;
    //        Reader = reader;
    //    }
    //    public readonly IntPtr Writer;
    //    public readonly IntPtr Reader;

    //    public bool IsEmpty
    //    {
    //        get { return Writer == IntPtr.Zero || Reader == IntPtr.Zero; }
    //    }
    //}
}
