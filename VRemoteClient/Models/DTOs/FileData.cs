using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteClient.Models.DTOs
{
    public class FileData
    {
        public string Filename { get; set; } = string.Empty;
        public string FileSize { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public byte[] Data { get; set; } = null;
    }
}
