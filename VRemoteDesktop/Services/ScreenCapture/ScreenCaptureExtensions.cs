using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Models;
using VRemoteDesktop.Utils;
using static VRemoteDesktop.Utils.Logger;

namespace VRemoteDesktop.Services.ScreenCapture
{
    public interface IScreenCaptureExtensions
    {
        byte[] RawScreenToScreenData(byte[] data);
        List<ScreenRegion> RawChunksToRegions(byte[] data);
        byte[] RawScreenToScreenDataWithoutChecksum(byte[] data);
        List<ScreenRegion> RawChunksToRegionsWithoutChecksum(byte[] data);
        Bitmap WriteToBitmap(byte[] data);
        Rectangle MergeRegions(Graphics g, List<ScreenRegion> regions);
        void Dispose();
    }
    public class ScreenCaptureExtensions: IScreenCaptureExtensions, IDisposable
    {
        private bool _disposed;
        private readonly object _lock;
        public ScreenCaptureExtensions() 
        {
            _disposed = false;
            _lock = new object();
        }
        public byte[] RawScreenToScreenDataWithoutChecksum(byte[] data)
        {
            try
            {
                var compressedLength = data.Length;
                var compressedData = new byte[compressedLength];
                Buffer.BlockCopy(data, 0, compressedData, 0, compressedLength);

                byte[] screenData = ByteArrayHelper.DeCompressDeflate(compressedData).GetResult();
                return screenData;
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "Error processing screen data");
            }
            return new byte[0];
        }
        public List<ScreenRegion> RawChunksToRegionsWithoutChecksum(byte[] data)
        {
            List<ScreenRegion> regions = new List<ScreenRegion>();
            try
            {
                var compressedLength = data.Length;
                var compressedData = new byte[compressedLength];
                Buffer.BlockCopy(data, 0, compressedData, 0, compressedLength);
                byte[] chunksDecompressed = ByteArrayHelper.DeCompressDeflate(compressedData).GetResult();

                int offset = 0;
                while (offset < chunksDecompressed.Length)
                {
                    if (offset + DefaultScreen.DEFAULT_CHUNK_HEADER_LENGTH > chunksDecompressed.Length)
                        break;

                    int length = BitConverter.ToInt32(chunksDecompressed, offset + 0);
                    int x = BitConverter.ToInt32(chunksDecompressed, offset + 4);
                    int y = BitConverter.ToInt32(chunksDecompressed, offset + 8);
                    int width = BitConverter.ToInt32(chunksDecompressed, offset + 12);
                    int height = BitConverter.ToInt32(chunksDecompressed, offset + 16);

                    if (offset + DefaultScreen.DEFAULT_CHUNK_HEADER_LENGTH + length > chunksDecompressed.Length)
                        break;

                    byte[] chunk = new byte[length];
                    Buffer.BlockCopy(chunksDecompressed, offset + DefaultScreen.DEFAULT_CHUNK_HEADER_LENGTH, chunk, 0, length);

                    offset += length + DefaultScreen.DEFAULT_CHUNK_HEADER_LENGTH;
                    regions.Add(new ScreenRegion
                    {
                        IsFullScreen = false,
                        Rectangle = new Rectangle(x, y, width, height),
                        Bytes = chunk
                    });
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "Error processing chunks data");
            }
            return regions;
        }
        public byte[] RawScreenToScreenData(byte[] data)
        {
            try
            {
                string stringHashReceived = Encoding.ASCII.GetString(data, 0, DefaultValue.SHA_CHECKSUM_LENGTH);

                var compressedLength = data.Length - DefaultValue.SHA_CHECKSUM_LENGTH;
                var compressedData = new byte[compressedLength];
                Buffer.BlockCopy(data, DefaultValue.SHA_CHECKSUM_LENGTH, compressedData, 0, compressedLength);

                string screenHash = StringHelper.SHAHash(compressedData);

                if (string.Compare(stringHashReceived, screenHash) == 0)
                {
                    byte[] screenData = ByteArrayHelper.DeCompressDeflate(compressedData).GetResult();
                    return screenData;
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "Error processing screen data");
            }
            return new byte[0];
        }
        public List<ScreenRegion> RawChunksToRegions(byte[] data)
        {
            List<ScreenRegion> regions = new List<ScreenRegion>();
            try
            {
                string stringHashReceived = Encoding.ASCII.GetString(data, 0, DefaultValue.SHA_CHECKSUM_LENGTH);

                var compressedLength = data.Length - DefaultValue.SHA_CHECKSUM_LENGTH;
                var compressedData = new byte[compressedLength];
                Buffer.BlockCopy(data, DefaultValue.SHA_CHECKSUM_LENGTH, compressedData, 0, compressedLength);

                string screenHash = StringHelper.SHAHash(compressedData);

                if (string.Compare(stringHashReceived, screenHash) == 0)
                {
                    byte[] chunksDecompressed = ByteArrayHelper.DeCompressDeflate(compressedData).GetResult();

                    int offset = 0;
                    while (offset < chunksDecompressed.Length)
                    {
                        if (offset + DefaultScreen.DEFAULT_CHUNK_HEADER_LENGTH > chunksDecompressed.Length)
                            break;

                        int length = BitConverter.ToInt32(chunksDecompressed, offset + 0);
                        int x = BitConverter.ToInt32(chunksDecompressed, offset + 4);
                        int y = BitConverter.ToInt32(chunksDecompressed, offset + 8);
                        int width = BitConverter.ToInt32(chunksDecompressed, offset + 12);
                        int height = BitConverter.ToInt32(chunksDecompressed, offset + 16);

                        if (offset + DefaultScreen.DEFAULT_CHUNK_HEADER_LENGTH + length > chunksDecompressed.Length)
                            break;

                        byte[] chunk = new byte[length];
                        Buffer.BlockCopy(chunksDecompressed, offset + DefaultScreen.DEFAULT_CHUNK_HEADER_LENGTH, chunk, 0, length);

                        offset += length + DefaultScreen.DEFAULT_CHUNK_HEADER_LENGTH;
                        regions.Add(new ScreenRegion
                        {
                            IsFullScreen = false,
                            Rectangle = new Rectangle(x, y, width, height),
                            Bytes = chunk
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "Error processing chunks data");
            }
            return regions;
        }
        public Bitmap WriteToBitmap(byte[] data)
        {
            using (MemoryStream stream = new MemoryStream(data))
            {
                Bitmap image = (Bitmap)Image.FromStream(stream);

                return image;
            }
        }
        public Rectangle MergeRegions(Graphics g, List<ScreenRegion> regions)
        {
            Rectangle dirtyRegion = Rectangle.Empty;
            lock (_lock)
            {
                for (int i = 0; i < regions.Count; i++)
                {
                    if (regions[i].Rectangle == null || regions[i].Bytes == null)
                        continue;
                    try
                    {
                        using (MemoryStream ms = new MemoryStream(regions[i].Bytes))
                        {
                            using (Bitmap chunkBitmap = new Bitmap(ms))
                            {
                                g.DrawImage(chunkBitmap, regions[i].Rectangle);

                                // merge dirty region
                                dirtyRegion = Rectangle.Union(dirtyRegion, regions[i].Rectangle);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.ForContext("FileName", "FormRemote").Warning(ex, "Chunks:Draw block error");
                    }
                }
            }
            return dirtyRegion;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_disposed) return;
                //TODO
            }
        }
    }
}
