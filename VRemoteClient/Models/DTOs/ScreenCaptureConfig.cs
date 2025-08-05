using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteClient.Models.DTOs
{
    public class ScreenCaptureConfig
    {
        public int ChunkSize { get; set; } = 8192;
        public int DefaultFrameRate { get; set; } = 20;
    }
}
