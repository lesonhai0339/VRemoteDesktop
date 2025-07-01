using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemoteClient.Remote
{
    public class ScreenChunkEntity
    {
        public List<Rectangle> Rects { get; set; }
        public List<byte[]> Data { get; set; }
    }
}
