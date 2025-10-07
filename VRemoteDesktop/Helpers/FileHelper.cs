using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VRemoteDesktop.Helpers
{
    public class StreamingFile
    {
        public DateTimeOffset DateTimeOffset { get; set; }
        public FileStream Stream { get; set; }  
    }
    internal class FileHelper
    {
        private static readonly ConcurrentDictionary<string, StreamingFile> _filesStreaming = new ConcurrentDictionary<string, StreamingFile>();
        private static System.Threading.Timer _timer = new System.Threading.Timer(CleanupFileStreaming, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        static FileHelper()
        {
            AppDomain.CurrentDomain.ProcessExit += (s, e) => CleanupAllStreams();
        }
        private static void CleanupAllStreams()
        {
            foreach (var kvp in _filesStreaming)
            {
                try
                {
                    kvp.Value.Stream?.Dispose();
                }
                catch { }
            }

            _filesStreaming.Clear();
        }
        private static void CleanupFileStreaming(object state)
        {
            foreach(var fileStreaming in _filesStreaming)
            {
                if(DateTimeOffset.UtcNow - fileStreaming.Value.DateTimeOffset > TimeSpan.FromMinutes(5)) //Close file stream after 5 minutes of inactivity
                {
                    try
                    {
                        if (_filesStreaming.TryRemove(fileStreaming.Key, out var stream))
                        {
                            stream.Stream.Dispose();
                        }
                    }
                    catch { }
                }
            }
        }
        private static readonly object _lock = new object();
        private static string DefaultFilter =
                "Text files (*.txt)|*.txt|" +
                "Word documents (*.doc;*.docx)|*.doc;*.docx|" +
                "Excel files (*.xls;*.xlsx)|*.xls;*.xlsx|" +
                "PDF files (*.pdf)|*.pdf|" +
                "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|" +
                "Executable files (*.exe)|*.exe|" +
                "ZIP archives (*.zip)|*.zip|" +
                "All files (*.*)|*.*";
        [DllImport("Shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO 
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);
        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        public static Icon GetFileIconFromFileExtension(string fileExtension)
        {
            SHFILEINFO shInfo = new SHFILEINFO();
            SHGetFileInfo(
                fileExtension,
                FILE_ATTRIBUTE_NORMAL,
                ref shInfo,
                (uint)Marshal.SizeOf(typeof(SHFILEINFO)),
                SHGFI_ICON | SHGFI_USEFILEATTRIBUTES);
            if(shInfo.hIcon == IntPtr.Zero)
            {
                return null;
            }
            return Icon.FromHandle(shInfo.hIcon);
        }
        public static string CreateFileChecksum(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}", filePath);

            using (var stream = new BufferedStream(File.OpenRead(filePath), 1200000))
            {
                MD5 md5 = MD5.Create();
                byte[] hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }     
        public static byte[] GetChunkFileDataByOffset(string filePath, long offset, int size = 8192)
        {
            if (!File.Exists(filePath))
                throw new ArgumentException(string.Format("Does not existed {0}", filePath));

            if (offset < 0)
                throw new ArgumentException("Offset cannot be negative");

            byte[] data = new byte[size];
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                fs.Seek(offset, SeekOrigin.Begin);
                int bytesRead = fs.Read(data, 0, size);
                if (bytesRead < size)
                {
                    Array.Resize(ref data, bytesRead);
                }
            }
            return data.ToArray();
        }
        public static int GetChunkFileDataByOffset(string filePath, long fileOffset, ref byte[] buffer, int size = 8192)
        {
            if (!File.Exists(filePath))
                throw new ArgumentException(string.Format("Does not existed {0}", filePath));

            if (fileOffset < 0)
                throw new ArgumentException("Offset cannot be negative");

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                fs.Seek(fileOffset, SeekOrigin.Begin);
                int bytesRead = fs.Read(buffer, 0, size);
                return bytesRead;
            }
        }
        public static int GetChunkFileDataByOffset(FileStream fs, long fileOffset, ref byte[] buffer, int bufferOffset, int size = 8192)
        {
            if (fileOffset < 0)
                throw new ArgumentException("Offset cannot be negative");

            fs.Seek(fileOffset, SeekOrigin.Begin);
            int totalBytesRead = 0;
            int remainingBytes = size;

            while (totalBytesRead < size && remainingBytes > 0)
            {
                int bytesRead = fs.Read(buffer, bufferOffset + totalBytesRead, remainingBytes);
                if (bytesRead == 0)
                    break;
                totalBytesRead += bytesRead;
                remainingBytes -= bytesRead;
            }
            return totalBytesRead;
        }
        public static int CopyFileDataByOffset(string filePath, long fileOffset, ref byte[] buffer, int bufferOffset, int size = 8192)
        {
            if (!File.Exists(filePath))
                throw new ArgumentException(string.Format("Does not existed {0}", filePath));
            if (fileOffset < 0)
                throw new ArgumentException("Offset cannot be negative");

            try
            {
                if (_filesStreaming.TryGetValue(filePath, out var fs))
                {
                    fs.Stream.Seek(fileOffset, SeekOrigin.Begin);
                    int totalBytesRead = 0;
                    int remainingBytes = size;

                    while (totalBytesRead < size && remainingBytes > 0)
                    {
                        int bytesRead = fs.Stream.Read(buffer, bufferOffset + totalBytesRead, remainingBytes);
                        if (bytesRead == 0)
                            break;
                        totalBytesRead += bytesRead;
                        remainingBytes -= bytesRead;
                    }
                    return totalBytesRead;
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }
        //Can improve by using something like private ConcurrentDictionary<string, FileStream> _curStreams; at VChatAttachmentService
        //To keep fileStream open util copy full or timeout
        public static int GetChunkFileDataByOffset(string filePath, long fileOffset, ref byte[] buffer, int bufferOffset, int size = 8192)
        {
            if (!File.Exists(filePath))
                throw new ArgumentException(string.Format("Does not existed {0}", filePath));

            if (fileOffset < 0)
                throw new ArgumentException("Offset cannot be negative");

            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    fs.Seek(fileOffset, SeekOrigin.Begin);
                    int totalBytesRead = 0;
                    int remainingBytes = size;
                    while (totalBytesRead < size && remainingBytes > 0)
                    {
                        int bytesRead = fs.Read(buffer, bufferOffset + totalBytesRead, remainingBytes);
                        if (bytesRead == 0)
                            break;

                        totalBytesRead += bytesRead;
                        remainingBytes -= bytesRead;
                    }

                    if(_filesStreaming.TryGetValue(filePath, out var streamingFile))
                    {
                        //Update time active for this stream
                        streamingFile.DateTimeOffset = DateTimeOffset.UtcNow;   
                    } 

                    return totalBytesRead;
                }
            }
            catch
            {
                return 0;
            }
        }
        public static long CalculateChunkNumber(long dataSize, int chunkSize = 8192)
        {
            if (dataSize <= 0)
                return 0;

            if (chunkSize <= 0)
                throw new InvalidOperationException("Data size and chunk size must be positive");

            return (dataSize + chunkSize - 1) / chunkSize;
        }
        public static FileInfo GetFileInfo(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}", filePath);

            return new FileInfo(filePath);
        }
        public static void InitializeFileTransfer(FileStream stream, string savePath)
        {
            lock (_lock)
            {
                stream?.Dispose();
                stream = new FileStream(savePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
            }
        }
        public static FileStream OpenStream(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(filePath + " is null", nameof(filePath)); 
            if (!File.Exists(filePath))
                throw new ArgumentNullException("File does not existed", nameof(filePath));

            try
            {
                FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                _filesStreaming.TryAdd(filePath, new StreamingFile
                {
                    Stream = fs,
                    DateTimeOffset = DateTimeOffset.UtcNow
                });
                return fs;
            }
            catch(Exception ex)
            {
                throw new InvalidOperationException("Cannot open file stream", ex);
            }
        }
        public static FileStream ReadFileStream(string savePath)
        {
            if (string.IsNullOrWhiteSpace(savePath))
                throw new ArgumentNullException(savePath + " is null", nameof(savePath));
            if (!File.Exists(savePath))
                throw new ArgumentNullException("File does not existed", nameof(savePath));
            try
            {
                FileStream fs = new FileStream(savePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                _filesStreaming.TryAdd(savePath, new StreamingFile
                {
                    Stream = fs,
                    DateTimeOffset = DateTimeOffset.UtcNow
                });
                return fs;
            }
            catch(Exception ex)
            {
                throw new InvalidOperationException("Cannot open file stream", ex); 
            }
        }
        public static FileStream CreateFileStream(string savePath)
        {
            if (string.IsNullOrWhiteSpace(savePath))
                throw new ArgumentNullException(savePath + " is null", nameof(savePath));

            try
            {
                FileStream fs = new FileStream(savePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);

                _filesStreaming.TryAdd(savePath, new StreamingFile
                {
                    Stream = fs,
                    DateTimeOffset = DateTimeOffset.UtcNow
                });
                return fs;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Cannot open file stream", ex);
            }
        }
        public static void WriteToFile(FileStream fs, int offset, byte[] data, bool flush = true)
        {
            if (fs == null)
                throw new ArgumentException("FileStream cannot be null.", nameof(fs));
            if (data == null)
                throw new ArgumentException("data cannot be null.", nameof(data));

            lock (fs)
            {
                try
                {
                    fs.Seek(offset, SeekOrigin.Begin);
                    fs.Write(data, 0, data.Length);
                    
                    if(flush)
                        fs.Flush();

                }
                catch (IOException ex)
                {
                    throw new InvalidOperationException($"Failed to write to file at offset {offset}", ex);
                }
            }
        }
        public static void WriteToFile(string path, string content)
        {

            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("File path cannot be null or empty.", nameof(path));

            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("Content cannot be null or empty.", nameof(content));

            lock (_lock)
            {
                try
                {
                    using (StreamWriter writer = new StreamWriter(path, true))
                    {
                        writer.Write(content);
                    }
                    if (_filesStreaming.TryGetValue(path, out var fileStreaming))
                    {
                        fileStreaming.DateTimeOffset = DateTimeOffset.UtcNow;
                    }
                }
                catch (IOException ex)
                {
                    throw new InvalidOperationException($"Failed to write to file: {path}", ex);
                }
            }
        }
        public static void WriteToFile(string path, int offset, byte[] data)
        {

            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("File path cannot be null or empty.", nameof(path));

            if (data == null)
                throw new ArgumentException("data cannot be null.", nameof(data));

            lock (_lock)
            {
                try
                {
                    using (FileStream fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write))
                    {
                        fs.Seek(offset, SeekOrigin.Begin);
                        fs.Write(data, 0, data.Length);
                    }

                    if(_filesStreaming.TryGetValue(path, out var fileStreaming))
                    {
                        fileStreaming.DateTimeOffset = DateTimeOffset.UtcNow;   
                    }
                }
                catch (IOException ex)
                {
                    throw new InvalidOperationException($"Failed to write to file: {path}", ex);
                }
            }
        }
        public static bool CloseStream(string filePath)
        {
            if(string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(filePath + " is null", nameof(filePath));

            try
            {
                if (_filesStreaming.TryRemove(filePath, out var stream))
                {
                    stream.Stream.Dispose();
                    return true;
                }

                if (File.Exists(filePath))
                {
                    using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                    }
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
        public static Icon GetIconByFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return null;

            string tempFilePath = Path.Combine(Path.GetTempPath(), fileName);
            if (!File.Exists(tempFilePath))
                File.WriteAllBytes(tempFilePath, new byte[0]);

            FileInfo info = new FileInfo(tempFilePath);
            return Icon.ExtractAssociatedIcon(tempFilePath);
        }
        public static string OpenFileDialogAndSaveFile(string fileName, string filter = null, int defaultFilterIndex = 1)
        {
            using (var dialog = new SaveFileDialog())
            {
                // Set default file name
                if (!string.IsNullOrWhiteSpace(fileName))
                    dialog.FileName = fileName;

                dialog.Filter = string.IsNullOrWhiteSpace(filter) ? DefaultFilter : filter;
                dialog.FilterIndex = defaultFilterIndex;

                return dialog.ShowDialog() == DialogResult.OK
                       && !string.IsNullOrWhiteSpace(dialog.FileName)
                    ? dialog.FileName
                    : null;
            }
        }
        public static string OpenFileDialogAndGetFilePath()
        {
            using (var dialog = new OpenFileDialog())
            {
                DialogResult result = dialog.ShowDialog();
                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    return dialog.FileName;
                }
                else
                {
                    return null;
                }
            }
        }
        public static string OpenDirectoryDialogAndGetDirectoryPath()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                DialogResult result = dialog.ShowDialog();
                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    return dialog.SelectedPath;
                }
                else
                {
                    return null;
                }
            }
        }
        public static bool RemoveFileByFilePath(string filePath)
        {
            if (!File.Exists(filePath)) return false;
            try
            {
                using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                File.Delete(filePath);
                return true;
            }
            catch
            {
                //File opened on another process
                return false;
            }
        }
    }
}
