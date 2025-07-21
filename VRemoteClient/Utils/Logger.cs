using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace VRemoteClient.Utils
{
    public static class Log
    {
        private static readonly Logging _logger = new Logging();

        static Log()
        {
            _logger.WriteTo("Logs");
        }

        public static Logging ForContext(string type, string name)
        {
            return _logger.ForContext(type, name);
        }
    }
    public class Logging
    {
        private string _filePath;
        private string _template;
        private string _context;

        public Logging WriteTo(string filePath, string template = null)
        {
            _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePath);
            _template = template ?? "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Context} {Message}{NewLine}{Exception}";
            return this;
        }

        public Logging ForContext(string type, string name)
        {
            _context = $"{type}:{name}";
            return this;
        }

        public void Info(Exception ex, string message) => Write("INFO", message, ex);
        public void Warning(Exception ex, string message) => Write("WARN", message, ex);
        public void Error(Exception ex, string message) => Write("ERROR", message, ex);
        public void Fatal(Exception ex, string message) => Write("FATAL", message, ex);

        public void Info(string message) => Write("INFO", message);
        public void Warning(string message) => Write("WARN", message);
        public void Error(string message) => Write("ERROR", message);
        public void Fatal(string message) => Write("FATAL", message);


        public void Info(string message, string message1) => Write("INFO", string.Format("{0} {1}", message, message1));
        public void Warning(string message, string message1) => Write("WARN", string.Format("{0} {1}", message, message1));
        public void Error(string message, string message1) => Write("ERROR", string.Format("{0} {1}", message, message1));
        public void Fatal(string message, string message1) => Write("FATAL", string.Format("{0} {1}", message, message1));


        private void Write(string level, string message, Exception ex = null)
        {
            if (string.IsNullOrEmpty(_filePath))
                throw new InvalidOperationException("Log path is not configured. Call WriteTo() first.");

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

            string log = _template
                .Replace("{Timestamp:yyyy-MM-dd HH:mm:ss}", timestamp)
                .Replace("{Level}", level)
                .Replace("{Context}", _context ?? "")
                .Replace("{Message}", message ?? "")
                .Replace("{Exception}", ex?.ToString() ?? "")
                .Replace("{NewLine}", Environment.NewLine);

            using (var writer = new StreamWriter(_filePath, true))
            {
                writer.WriteLine(log);
            }
        }
    }
}
