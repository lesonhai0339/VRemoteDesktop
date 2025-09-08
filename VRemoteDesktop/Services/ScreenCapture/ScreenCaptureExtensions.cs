using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Models;
using static VRemoteDesktop.Utils.Logger;

namespace VRemoteDesktop.Services.ScreenCapture
{
    public class ScreenCaptureExtensions
    {
        private readonly object _lock = new object();
        public ScreenCaptureExtensions() { }
        public byte[] RawScreenToScreenData(byte[] data)
        {
            try
            {
                string stringHashReceived = Encoding.ASCII.GetString(data, 0, 40);

                var compressedLength = data.Length - 40;
                var compressedData = new byte[compressedLength];
                Buffer.BlockCopy(data, 40, compressedData, 0, compressedLength);

                string screenHash = StringHelper.SHAHash(compressedData);

                if (string.Compare(stringHashReceived, screenHash) == 0)
                {
                    byte[] screenData = ByteArrayHelper.DecompressGzip(compressedData).GetResult();
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
                string stringHashReceived = Encoding.ASCII.GetString(data, 0, 40);

                var compressedLength = data.Length - 40;
                var compressedData = new byte[compressedLength];
                Buffer.BlockCopy(data, 40, compressedData, 0, compressedLength);

                string screenHash = StringHelper.SHAHash(compressedData);

                if (string.Compare(stringHashReceived, screenHash) == 0)
                {
                    byte[] chunksDecompressed = ByteArrayHelper.DecompressGzip(compressedData).GetResult();

                    int offset = 0;
                    while (offset < chunksDecompressed.Length)
                    {
                        if (offset + 20 > chunksDecompressed.Length)
                            break;

                        int length = BitConverter.ToInt32(chunksDecompressed, offset + 0);
                        int x = BitConverter.ToInt32(chunksDecompressed, offset + 4);
                        int y = BitConverter.ToInt32(chunksDecompressed, offset + 8);
                        int width = BitConverter.ToInt32(chunksDecompressed, offset + 12);
                        int height = BitConverter.ToInt32(chunksDecompressed, offset + 16);

                        if (offset + 20 + length > chunksDecompressed.Length)
                            break;

                        byte[] chunk = new byte[length];
                        Buffer.BlockCopy(chunksDecompressed, offset + 20, chunk, 0, length);

                        offset += length + 20;
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
    }
}
