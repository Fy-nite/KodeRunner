using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using KodeRunner;

namespace Tuvalu.logger
{
    public class Logger
    {
        private static readonly SemaphoreSlim _logLock = new SemaphoreSlim(1, 1);

        static readonly string time = DateTime.Now.ToString("yyyy-MM-dd");
        static readonly string logPath = Path.Combine(Core.RootDir, Core.LogDir, $"{time}.log");
        static readonly int restartCount = 0;

        static Logger()
        {
            if (File.Exists(logPath))
            {
                restartCount = File.ReadAllLines(logPath)
                    .Count(line => line.Contains("=== Restart"));
                AppendRestartSeparator();
            }
            else
            {
                AppendRestartSeparator();
            }
        }

        private static void AppendRestartSeparator()
        {
            // if the restart count is 0 then we just add start of program separator
            if (restartCount == 0)
            {
                string separator = $"=== Start of program ===\n";
                File.AppendAllText(logPath, separator);
            }
            else
            {
                string separator = $"=== Restart {restartCount + 1} ===\n";
                File.AppendAllText(logPath, separator);
            }
            // string separator = $"=== Restart {restartCount + 1} ===\n";
            // File.AppendAllText(logPath, separator);
        }

        public struct LogEntry
        {
            public string Message;
            public string Level;
            public string Timestamp;
        }

        public static void Log(LogEntry entry)
        {
            string logEntry = $"{entry.Timestamp} - {entry.Level}: {entry.Message}\n";
            File.AppendAllText(logPath, logEntry);
        }

        public static void Log(string message, string level)
        {
            var entry = new LogEntry
            {
                Message = message,
                Level = level,
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            };
            Log(entry);
        }

        public static void Log(string message)
        {
            Log(message, "INFO");
        }

        public static void Log(Exception ex)
        {
            LogEntry entry = new LogEntry
            {
                Message = ex.Message,
                Level = "ERROR",
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            };
            Log(entry);
        }

        public static void Log(string message, Exception ex)
        {
            LogEntry entry = new LogEntry
            {
                Message = $"{message}: {ex.Message}",
                Level = "ERROR",
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            };
            Log(entry);
        }

        public static void Log(string message, string level, string customLogPath)
        {
            LogEntry entry = new LogEntry
            {
                Message = message,
                Level = level,
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            };
            string logEntry = $"{entry.Timestamp} - {entry.Level}: {entry.Message}\n";
            File.AppendAllText(customLogPath, logEntry);
        }

        public static async Task LogAsync(LogEntry entry)
        {
            await _logLock.WaitAsync();
            try
            {
                string logEntry = $"{entry.Timestamp} - {entry.Level}: {entry.Message}\n";
                await File.AppendAllTextAsync(logPath, logEntry);
            }
            finally
            {
                _logLock.Release();
            }
        }

        public static async Task LogAsync(string message)
        {
            await LogAsync(
                new LogEntry
                {
                    Message = message,
                    Level = "INFO",
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                }
            );
        }

        public static async Task LogAsync(Exception ex)
        {
            await LogAsync(
                new LogEntry
                {
                    Message = ex.Message,
                    Level = "ERROR",
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                }
            );
        }
    }
}
