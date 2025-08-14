using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteClient.Models.DTOs
{
    public class FileData
    {
        public string Id { get; set; } = string.Empty;
        public string Filename { get; set; } = string.Empty;
        public long FileSize { get; set; } 
        public string Checksum { get;set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public int NumberOfChunks { get; set; } = 0;
        public int ChunkSize { get; set; } = 8192;
        public int CurrentChunkIndex { get; set; } = 0;
        public List<byte[]> Chunks { get; set; } = new List<byte[]>();
        public bool IsCompleted { get; set; } = false;


    }
}
