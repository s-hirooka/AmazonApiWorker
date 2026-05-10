using System;
using System.IO;
using System.Text;

namespace PaApiWorker
{
    public sealed class AppLogger
    {
        private readonly string _logFilePath;
        private readonly object _sync = new object();

        public AppLogger(string logDirectory)
        {
            if (string.IsNullOrWhiteSpace(logDirectory))
                throw new ArgumentException("logDirectory is required.", nameof(logDirectory));

            Directory.CreateDirectory(logDirectory);
            _logFilePath = Path.Combine(logDirectory, $"paapi_worker_{DateTime.Now:yyyyMMdd}.log");
        }

        public void Info(string message) => Write("INFO", message);

        public void Warn(string message) => Write("WARN", message);

        public void Error(string message) => Write("ERROR", message);

        public void Error(Exception ex, string? message = null)
        {
            var text = message == null
                ? ex.ToString()
                : message + Environment.NewLine + ex;
            Write("ERROR", text);
        }

        private void Write(string level, string message)
        {
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";

            lock (_sync)
            {
                File.AppendAllText(_logFilePath, line + Environment.NewLine, Encoding.UTF8);
            }

            Console.WriteLine(line);
        }
    }
}