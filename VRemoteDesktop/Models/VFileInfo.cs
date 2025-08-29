using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Models
{
    public class VFileInfo
    {
        public VFileInfo() { }
        public VFileInfo(string fileExtension, string filename, long fileSize)
        {
            FileExtension = fileExtension;
            Filename = filename;
            FileSize = fileSize;
        }

        public string FileExtension { get; set; }
        public string Filename { get; set; }
        public long FileSize { get; set; }
    }
}
