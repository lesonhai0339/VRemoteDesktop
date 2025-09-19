using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Models
{
    public class ChunkFileInfo
    {
        public ChunkFileInfo(string fileId, string filePath, long offset, int chunkSize)
        {
            FileId = fileId;
            FilePath = filePath;
            Offset = offset;
            ChunkSize = chunkSize;
        }
    
        public string FileId { get; set; }
        public string FilePath { get; set; }
        public long Offset { get; set; }
        public int ChunkSize { get; set; }  
    }
}
