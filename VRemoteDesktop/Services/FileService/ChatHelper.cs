using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Services.FileService
{
    public static class ChatHelper
    {
        private static readonly object _lock = new object();   
        public static string GetLastMessage(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                    throw new ArgumentNullException("File path cannot be null or empty");

                if (!File.Exists(filePath))
                    throw new InvalidOperationException("Does not existed " + filePath);

                lock (_lock)
                {
                    using (StreamReader reader = new StreamReader(filePath))
                    {
                        return reader.ReadLine().Last().ToString();
                    }
                }
            }
            catch(IOException ex)
            {

            }
            catch(Exception ex)
            {

            }
            return string.Empty;
        }
        public static void WriteMessage(string filePath, string msg)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                    throw new ArgumentNullException("File path cannot be null or empty");
                if(!File.Exists(filePath))
                    throw new InvalidOperationException("Does not existed " + filePath);

                lock (_lock)
                {
                    using (StreamWriter write = new StreamWriter(filePath, true))
                    {
                        write.WriteLine(msg);
                        write.Flush();
                    }
                }
            }
            catch(IOException ex)
            {

            }
            catch(Exception ex)
            {

            }
        }
    }
}
