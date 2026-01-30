using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using static VRemoteDesktop.Interop.Win32Apis;
using System.Threading;
using System.Windows.Forms;
using VRemoteDesktop.Enums;
using static VRemoteDesktop.Services.ScreenCapture.Interop.CaptureApi;
using VRemoteDesktop.Services.ScreenCapture.Interop;
using VRemoteDesktop.Services.ScreenCapture.Utils;

namespace VRemoteDesktop.Services.ScreenCapture
{
    public abstract class VScreen: IDisposable
    {
        //protected const uint DIB_RGB_COLORS = 0;
        //protected const int BYTE_PER_PIXEL = 3;
        //protected const int REGION_SIZE = 16;

        public VScreen()
        {
        }
        public int GetScreenDataLength(List<Rectangle> rects, int bytePerPixel)
        {
            return rects.Sum(x => GetScreenDataLength(x.Width , x.Height , bytePerPixel)); //Only send raw bytes + 16 is header
        }
        public int GetScreenDataLength(int width, int height, int bytePerPixel)
        {
            return (width * height * bytePerPixel) + 16; //Only send raw bytes + 16 is header
        }
        /// <summary>
        /// If bytePerPixel is 1 bit per pixel
        /// </summary>
        /// <param name="width"></param>
        /// <param name="bytePerPixel"></param>
        /// <returns></returns>
        public virtual int GetStride1(int width, int bytePerPixel)
        {
            return (((width * bytePerPixel * 8) + 31) & ~31) >> 3;
        }
        public virtual int GetStride(int width, int bytePerPixel)
        {
            return ((width * bytePerPixel) + 3) & ~3;
        }
        public virtual BITMAPINFO InitBitmapInfo(
            int width,
            int height,
            ushort bitPerPixel = 24,
            uint compression = 0)
        {
            var bmi = new BITMAPINFO();
            bmi.Header.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            bmi.Header.biWidth = width;
            bmi.Header.biHeight = -height;
            bmi.Header.biPlanes = 1;
            bmi.Header.biBitCount = bitPerPixel;
            bmi.Header.biCompression = compression;

            return bmi;
        }
        protected virtual void InitCaptureBuffer(
            ref IntPtr hBitmap,
            ref IntPtr memDC,
            ref IntPtr bits,
            IntPtr sectionPool,
            uint offset,
            IntPtr screenDCPtr,
            BITMAPINFO bmi,
            uint DIB_RGB_COLORS = 0)
        {
            IntPtr screenDC = CaptureApi.GetDC(screenDCPtr);
            hBitmap = CaptureApi.CreateDIBSection(
              screenDC,
              ref bmi,
              DIB_RGB_COLORS,
              out bits,
              sectionPool,
              offset);

            if (hBitmap == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                throw new Exception("CreateDIBSection failed with error: " + error);
            }

            memDC = CaptureApi.CreateCompatibleDC(screenDC);
            CaptureApi.SelectObject(memDC, hBitmap);

            CaptureApi.ReleaseDC(screenDCPtr, screenDC);
        }
        public virtual Rectangle[] InitRectangle(int width, int height, int regionSize = 16)
        {
            if (width < 0)
                throw new ArgumentNullException("width is required");
            if (height < 0)
                throw new ArgumentNullException("height is required");
            if (height < 0)
                throw new ArgumentNullException("regionSize must be > 0");

            int cols = (width + regionSize - 1) / regionSize;
            int rows = (height + regionSize - 1) / regionSize;

            Rectangle[] rectangles = new Rectangle[cols * rows];

            int index = 0;
            for (int i = 0; i < height; i += regionSize)
            {
                for (int j = 0; j < width; j += regionSize)
                {
                    int w = Math.Min(regionSize, width - j);
                    int h = Math.Min(regionSize, height - i);

                    rectangles[index++] = new Rectangle(j, i, w, h);
                }
            }
            return rectangles;
        }
        public virtual void InitFileMapping(ref IntPtr fileMappingPtr, uint pageProtect = 0x40, uint allocateSize = 30 * 1024 * 1024)
        {
            // Create shared memory for DIBSection https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-createfilemappinga
            // It is hSection in https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-createdibsection
            fileMappingPtr = CaptureApi.CreateFileMappingA(
                new IntPtr(-1),
                IntPtr.Zero,
                pageProtect, //PAGE_EXECUTE_READWRITE
                0,
                allocateSize, //Allocate 30MB shared memory
                null
                );
        }
        public virtual void CaptureToBuffer(IntPtr memDC, IntPtr screenDC, int x, int y, int width, int height)
        {
            CaptureApi.BitBlt(
                memDC,
                0, 0,
                width,
                height,
                screenDC,
                x,
                y,
                0x00CC0020); // SRCCOPY
            //CaptureApi.GdiFlush();
        }
        public unsafe virtual void CopySourceToDest(IntPtr source, IntPtr dest, int x, int y, int width, int height, int fullWidth, int bytePerPixel = 3)
        {
            int stride = GetStride1(fullWidth, bytePerPixel);
            int rowWidthInBytes = width * bytePerPixel;

            byte* src = (byte*)source;
            byte* dst = (byte*)dest;

            int startOffset = (y * stride) + (x * bytePerPixel);
            src += startOffset;
            dst += startOffset;

            for (int row = 0; row < height; row++)
            {
                CaptureApi.memcpy((IntPtr)dst, (IntPtr)src, (UIntPtr)rowWidthInBytes);

                src += stride;
                dst += stride;
            }
        }
        public virtual List<Rectangle> MergeRegions(List<Rectangle> regions, double threshold = 0.8)
        {
            if (regions == null || regions.Count == 0)
                return new List<Rectangle>();

            var dirtyRegionsSorted = regions.OrderBy(x => x.Top)
                .ThenBy(x => x.Left)
                .ToList();

            List<Rectangle> groups = new List<Rectangle>();

            // Using first rectangle as base
            var baseRect = dirtyRegionsSorted[0];

            // For-loop to try to merge other rectangles
            for (int i = 1; i < dirtyRegionsSorted.Count; i++)
            {
                // Get current rectangle    
                var rect = dirtyRegionsSorted[i];

                // Union base rectangle and current rectangle
                var area = Rectangle.Union(baseRect, rect);

                // Get acreage of union area
                var areaUnion = area.Width * area.Height;
                // Get acreage sum of base rectangle and current rectangle
                var areaSum = baseRect.Width * baseRect.Height + rect.Width * rect.Height;

                // Calculate ratio between union area and acreage sum
                var ratio = (double)areaSum / areaUnion;

                // If ratio > threshold, assign union area to base rectangle and continue try to merge next rectangle
                if (ratio > threshold)
                {
                    baseRect = area;
                }
                // Else, add base rectangle to groups result and assign current as a new base rectangle
                else
                {
                    groups.Add(baseRect);
                    baseRect = rect;
                }
            }

            // Finally, add last base rectangle to groups result
            groups.Add(baseRect);

            return groups;
        }
        public virtual List<Rectangle> MergeRegions(Rectangle[] regions, double threshold = 0.8)
        {
            if (regions == null || regions.Length == 0)
                return new List<Rectangle>();

            var dirtyRegionsSorted = regions.OrderBy(x => x.Top)
                .ThenBy(x => x.Left)
                .ToList();

            List<Rectangle> groups = new List<Rectangle>();

            // Using first rectangle as base
            var baseRect = dirtyRegionsSorted[0];

            // For-loop to try to merge other rectangles
            for (int i = 1; i < dirtyRegionsSorted.Count; i++)
            {
                // Get current rectangle    
                var rect = dirtyRegionsSorted[i];

                // Union base rectangle and current rectangle
                var area = Rectangle.Union(baseRect, rect);

                // Get acreage of union area
                var areaUnion = area.Width * area.Height;
                // Get acreage sum of base rectangle and current rectangle
                var areaSum = baseRect.Width * baseRect.Height + rect.Width * rect.Height;

                // Calculate ratio between union area and acreage sum
                var ratio = (double)areaSum / areaUnion;

                // If ratio > threshold, assign union area to base rectangle and continue try to merge next rectangle
                if (ratio > threshold)
                {
                    baseRect = area;
                }
                // Else, add base rectangle to groups result and assign current as a new base rectangle
                else
                {
                    groups.Add(baseRect);
                    baseRect = rect;
                }
            }

            // Finally, add last base rectangle to groups result
            groups.Add(baseRect);

            return groups;
        }
        public virtual unsafe void GetRegionsData(
            ref int offset,
            IntPtr destination,
            IntPtr source,
            int srcWidth,
            int regionX,
            int regionY,
            int regionWidth,
            int regionHeight,
            int bytePerPixel = 3)
        {
            //Get the base pointer
            byte* basePtr = (byte*)destination + offset;

            //Add header info of region(x,y,w,h)
            uint* d = (uint*)basePtr;
            *d++ = (uint)regionX;
            *d++ = (uint)regionY;
            *d++ = (uint)regionWidth;
            *d++ = (uint)regionHeight;

            //calculate stride of big screen and regions
            int srcStride = GetStride(srcWidth, bytePerPixel);
            uint dstStride = (uint)GetStride(regionWidth, bytePerPixel);

            unsafe
            {
                //get address of source and destination 
                byte* src = (byte*)source;
                byte* dst = (byte*)d;

                //for-loop to write row from source to destination
                for (int row = 0; row < regionHeight; row++)
                {
                    int sy = (row + regionY) * srcStride; //reverser screen data , see more at InitCaptureBuffer() ->  bmi.Header.biHeight = -height;
                    int sx = regionX * bytePerPixel; //offset in row

                    IntPtr srcAdd = (IntPtr)(src + sy + sx); //formular => address = base + rowOffset + colOffset 

                    IntPtr dstAdd = (IntPtr)(dst + (row * dstStride)); //formular => address = base + (row * rowStride) 

                    CaptureApi.memcpy(dstAdd, srcAdd, (UIntPtr)dstStride);
                }
            }
            offset += (int)(dstStride * regionHeight) + 16; //Data + header(16 bytes for x,y,w,h)
        }
        public virtual unsafe void GetRegionsData(
         ref int offset,
         byte[] destination,
         IntPtr source,
         int srcWidth,
         int regionX,
         int regionY,
         int regionWidth,
         int regionHeight,
         int bytePerPixel = 3)
        {
            fixed (byte* bst = destination)
            {
                byte* basePtr = bst + offset;
                //Add header info of region(x,y,w,h)
                uint* d = (uint*)basePtr;
                *d++ = (uint)regionX;
                *d++ = (uint)regionY;
                *d++ = (uint)regionWidth;
                *d++ = (uint)regionHeight;

                //calculate stride of big screen and regions
                int srcStride = GetStride1(srcWidth, bytePerPixel);
                uint dstStride = (uint)(regionWidth * bytePerPixel);

                if (offset + (dstStride * regionHeight) + 16 > destination.Length)
                {
                    throw new IndexOutOfRangeException("Buffer quá nhỏ để chứa dữ liệu vùng này!");
                }

                unsafe
                {
                    //get address of source and destination 
                    byte* src = (byte*)source;
                    byte* dst = (byte*)d;

                    //for-loop to write row from source to destination
                    for (int row = 0; row < regionHeight; row++)
                    {
                        int sy = (row + regionY) * srcStride; //reverser screen data , see more at InitCaptureBuffer() ->  bmi.Header.biHeight = -height;
                        int sx = regionX * bytePerPixel; //offset in row

                        IntPtr srcAdd = (IntPtr)(src + sy + sx); //formular => address = base + rowOffset + colOffset 

                        IntPtr dstAdd = (IntPtr)(dst + (row * dstStride)); //formular => address = base + (row * rowStride) 

                        CaptureApi.memcpy(dstAdd, srcAdd, (UIntPtr)dstStride);
                    }
                }
                offset += (int)(dstStride * regionHeight) + 16; //Data + header(16 bytes for x,y,w,h)
            }
        }
        //New
       public virtual unsafe void GetRegionsDataWithoutRectangle(
       ref int offset,
       IntPtr destination,
       IntPtr source,
       int srcWidth,
       int regionX,
       int regionY,
       int regionWidth,
       int regionHeight,
       int bytePerPixel = 3)
       {
            byte* dst = (byte*)destination + offset;
            //calculate stride of big screen and regions
            int srcStride = GetStride(srcWidth, bytePerPixel);
            uint dstStride = (uint)(regionWidth * bytePerPixel);

            unsafe
            {
                //get address of source and destination 
                byte* src = (byte*)source;

                //for-loop to write row from source to destination
                for (int row = 0; row < regionHeight; row++)
                {
                    int sy = (row + regionY) * srcStride;
                    int sx = regionX * bytePerPixel;

                    IntPtr srcAdd = (IntPtr)(src + sy + sx);

                    IntPtr dstAdd = (IntPtr)(dst + (row * dstStride));

                    CaptureApi.memcpy(dstAdd, srcAdd, (UIntPtr)dstStride);
                }
            }
            offset += (int)(dstStride * regionHeight);
        }
       public virtual unsafe void GetRegionsDataWithoutRectangle(
       ref int offset,
       byte[] destination,
       IntPtr source,
       int srcWidth,
       int regionX,
       int regionY,
       int regionWidth,
       int regionHeight,
       int bytePerPixel = 3)
        {
            fixed (byte* bst = destination)
            {
                byte* dst = bst + offset;              

                //calculate stride of big screen and regions
                int srcStride = GetStride(srcWidth, bytePerPixel);
                uint dstStride = (uint)(regionWidth * bytePerPixel);

                if (offset + (dstStride * regionHeight) > destination.Length)
                {
                    throw new IndexOutOfRangeException("Available buffer less than data");
                }

                unsafe
                {
                    //get address of source and destination 
                    byte* src = (byte*)source;

                    //for-loop to write row from source to destination
                    for (int row = 0; row < regionHeight; row++)
                    {
                        int sy = (row + regionY) * srcStride;
                        int sx = regionX * bytePerPixel;

                        IntPtr srcAdd = (IntPtr)(src + sy + sx);

                        IntPtr dstAdd = (IntPtr)(dst + (row * dstStride)); 

                        CaptureApi.memcpy(dstAdd, srcAdd, (UIntPtr)dstStride);
                    }
                }
                offset += (int)(dstStride * regionHeight);
            }
        }
        public virtual unsafe void CopyFullScreenSourceToDest(
          ref int offset,
          IntPtr destination,
          IntPtr source,
          int srcWidth,
          int regionX,
          int regionY,
          int regionWidth,
          int regionHeight,
          int bytePerPixel = 3)
        {
            byte* dst = (byte*)destination + offset;

            int srcStride = GetStride1(srcWidth, bytePerPixel);
            unsafe
            {
                byte* src = (byte*)source;

                for (int row = 0; row < regionHeight; row++)
                {
                    int sy = (row + regionY) * srcStride;
                    int sx = regionX * bytePerPixel;

                    IntPtr srcAdd = (IntPtr)(src + sy + sx);

                    IntPtr dstAdd = (IntPtr)(dst + (row * srcStride)); 

                    CaptureApi.memcpy(dstAdd, srcAdd, (UIntPtr)srcStride);
                }
            }
            offset += (int)(srcStride * regionHeight);
        }
        public virtual unsafe void GetFullScreenData(
            ref int offset,
            IntPtr destination,
            IntPtr source,
            int srcWidth,
            int regionX,
            int regionY,
            int regionWidth,
            int regionHeight,
            int bytePerPixel = 3)
        {
            byte* dst = (byte*)destination + offset;

            int srcStride = GetStride(srcWidth, bytePerPixel);
            uint dstStride = (uint)(regionWidth * bytePerPixel); //Only send raw data without padding

            unsafe
            {
                //get address of source and destination 
                byte* src = (byte*)source;

                //for-loop to write row from source to destination
                for (int row = 0; row < regionHeight; row++)
                {
                    int sy = (row + regionY) * srcStride; //reverser screen data , see more at InitCaptureBuffer() ->  bmi.Header.biHeight = -height;
                    int sx = regionX * bytePerPixel; //offset in row

                    IntPtr srcAdd = (IntPtr)(src + sy + sx); //formular => address = base + rowOffset + colOffset 

                    IntPtr dstAdd = (IntPtr)(dst + (row * dstStride)); //formular => address = base + (row * rowStride) 

                    CaptureApi.memcpy(dstAdd, srcAdd, (UIntPtr)dstStride);
                }
            }
            offset += (int)(dstStride * regionHeight);
        }
        public virtual unsafe void GetFullScreenData(
           ref int offset,
           byte[] destination,
           IntPtr source,
           int srcWidth,
           int regionX,
           int regionY,
           int regionWidth,
           int regionHeight,
           int bytePerPixel = 3)
        {
            fixed(byte* dstPtr = destination)
            {
                byte* dst = dstPtr + offset;
                int srcStride = GetStride(srcWidth, bytePerPixel);
                uint dstStride = (uint)(regionWidth * bytePerPixel); //Only send raw data without padding

                if (offset + (dstStride * regionHeight) + 16 > destination.Length)
                {
                    throw new IndexOutOfRangeException("Buffer quá nhỏ để chứa dữ liệu vùng này!");
                }

                unsafe
                {
                    //get address of source and destination 
                    byte* src = (byte*)source;

                    //for-loop to write row from source to destination
                    for (int row = 0; row < regionHeight; row++)
                    {
                        int sy = (row + regionY) * srcStride; //reverser screen data , see more at InitCaptureBuffer() ->  bmi.Header.biHeight = -height;
                        int sx = regionX * bytePerPixel; //offset in row

                        IntPtr srcAdd = (IntPtr)(src + sy + sx); //formular => address = base + rowOffset + colOffset 

                        IntPtr dstAdd = (IntPtr)(dst + (row * dstStride)); //formular => address = base + (row * rowStride) 

                        CaptureApi.memcpy(dstAdd, srcAdd, (UIntPtr)dstStride);
                    }
                }
                offset += (int)(dstStride * regionHeight);
            }
        }
        public virtual unsafe bool IsRegionChange(
            IntPtr oldScreen,
            IntPtr newScreen,
            int srcWidth,
            int regionX,
            int regionY,
            int regionWidth,
            int regionHeight,
            int bytePerPixel = 3)
        {
            int stride = GetStride(srcWidth, bytePerPixel);
            unsafe
            {
                byte* oBase = (byte*)oldScreen;
                byte* nBase = (byte*)newScreen;

                for (int row = 0; row < regionHeight; row++)
                {
                    //get the start off row
                    int sy = (row + regionY) * stride;
                    int sx = regionX * bytePerPixel;

                    byte* pOld = (oBase + sy + sx);
                    byte* pNew = (nBase + sy + sx);

                    for (int col = 0; col < regionWidth; col++)
                    {
                        if (pOld[0] != pNew[0] ||
                            pOld[1] != pNew[1] ||
                            pOld[2] != pNew[2])
                        {
                            return true;
                        }

                        pOld += bytePerPixel;
                        pNew += bytePerPixel;
                    }
                }
            }
            return false;
        }
        public virtual unsafe bool IsRegionChangeUseLong(
           IntPtr oldScreen,
           IntPtr newScreen,
           int srcWidth,
           int regionX,
           int regionY,
           int regionWidth,
           int regionHeight,
           int bytePerPixel = 3)
        {
            int stride = GetStride(srcWidth, bytePerPixel);
            unsafe
            {
                byte* oBase = (byte*)oldScreen;
                byte* nBase = (byte*)newScreen;

                int rowBytes = regionWidth * bytePerPixel;
                int longCount = rowBytes >> 3; 

                for (int row = 0; row < regionHeight; row++)
                {
                    int offset = (row + regionY) * stride + (regionX * bytePerPixel);

                    long* o = (long*)(oBase + offset);
                    long* n = (long*)(nBase + offset);

                    for (int i = 0; i < longCount; i++)
                    {
                        if (*(o + i) != *(n + i))
                        {
                            return true;
                        }
                    }

                    byte* bO = (byte*)(o + longCount);
                    byte* bN = (byte*)(n + longCount);
                    for (int i = 0; i < (rowBytes % 8); i++)
                    {
                        if (bO[i] != bN[i]) return true;
                    }
                }
            }
            return false;
        }
        public virtual unsafe bool IsRegionChangeInRange(
            int range,
            IntPtr oldScreen,
            IntPtr newScreen,
            int srcWidth,
            int regionX,
            int regionY,
            int regionWidth,
            int regionHeight,
            int bytePerPixel = 3)
        {
            int stride = GetStride(srcWidth, bytePerPixel);
            unsafe
            {
                byte* oBase = (byte*)oldScreen;
                byte* nBase = (byte*)newScreen;

                for (int row = 0; row < regionHeight; row++)
                {
                    //get the start off row
                    int sy = (row + regionY) * stride;
                    int sx = regionX * bytePerPixel;

                    byte* pOld = (oBase + sy + sx);
                    byte* pNew = (nBase + sy + sx);

                    for (int col = 0; col < regionWidth; col++)
                    {
                        if (Math.Abs(pOld[0] - pNew[0]) > range ||
                            Math.Abs(pOld[1] - pNew[1]) > range ||
                            Math.Abs(pOld[2] - pNew[2]) > range)
                            return true;

                        pOld += bytePerPixel;
                        pNew += bytePerPixel;
                    }
                }
            }
            return false;
        }
        public unsafe void ParsePacketToRegionsChange(byte[] packet, IntPtr dstPtr, int dstWidth, int dstHeight, int bytePerPixel = 3)
        {
            fixed (byte* pPacket = packet)
            {
                int offset = 0;
                int length = packet.Length;
                while (offset + 16 <= length)
                {
                    // get pointer to current position
                    byte* src = pPacket + offset;

                    // move to uint pointer, each d++ move 4 bytes
                    uint* d = (uint*)src;

                    int x = (int)*d++;
                    int y = (int)*d++;
                    int w = (int)*d++;
                    int h = (int)*d++;

                    // back to single byte pointer, each d++ move 1 byte
                    IntPtr srcRegion = (IntPtr)d;

                    offset += 16;

                    offset += MergeRegionToSource(srcRegion, x, y, w, h, dstPtr, dstWidth, dstHeight, bytePerPixel);
                }
            }
        }
        public virtual int MergeRegionToSource(IntPtr source, int x, int y, int width, int height, IntPtr destination, int dstWidth, int dstHeight, int bytePerPixel =3)
        {
            if (x < 0 || y < 0 || x + width > dstWidth || y + height > dstHeight)
                return 0;

            int srcStride = width * bytePerPixel; //No padding in source buffer
            int dstStride = GetStride(dstWidth, bytePerPixel);
            unsafe
            {
                byte* srcBase = (byte*)source;
                byte* dstBase = (byte*)destination;

                byte* dst = dstBase + (y * dstStride) + (x * bytePerPixel);
                for (int row = 0; row < height; row++)
                {
                    var srcPtr = srcBase + (row * srcStride);
                    var dstPtr = dst + (row * dstStride);

                    CaptureApi.memcpy((IntPtr)dstPtr, (IntPtr)srcPtr, (UIntPtr)srcStride);
                }
            }
            return height * srcStride;
        }

        public abstract void Dispose(bool disposing);
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);  
        }
        ~VScreen()
        {
            Dispose(false);
        }
    }
}
