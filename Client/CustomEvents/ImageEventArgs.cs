using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RemoteClient
{
    public class ImageEventArgs:EventArgs
    {
        public byte[] Data { get; }

        public ImageEventArgs(byte[] data)
        {
            Data = data;
        }
    }
    public class TextEventArgs : EventArgs
    {
        public string Data { get; }

        public TextEventArgs(string data)
        {
            Data = data;
        }
    }
}
