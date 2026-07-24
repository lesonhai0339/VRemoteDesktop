using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vsign4.VRemoteDesktop.Services.FileService.DTOs
{
    public class ChunkFileInfo
    {
        public ChunkFileInfo(string fileId, string filePath, long fileLength, long offset, int chunkSize)
        {
            FileId = fileId;
            FilePath = filePath;
            FileLength = fileLength;
            Offset = offset;
            ChunkSize = chunkSize;
        }

        public string FileId { get; set; }
        public string FilePath { get; set; }
        public long FileLength { get; set; }
        public long Offset { get; set; }
        public int ChunkSize { get; set; }
    }
}
