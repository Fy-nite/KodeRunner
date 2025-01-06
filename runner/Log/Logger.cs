using KodeRunner.Config;

namespace KodeRunner
{
    public static class Logger
    {
        private static readonly Configuration _config = Configuration.Load();
        private static readonly object _lock = new();
        private static string CurrentLogFile =>
            Path.Combine(
                Core.GetPath(_config.Logging.LogDirectory),
                $"log_{DateTime.Now:yyyy-MM-dd}.txt"
            );

        public static void Log(string message, string level = "Info")
        {
            if (!ShouldLog(level))
                return;

            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";

            if (_config.Logging.EnableConsoleLogging)
            {
                Terminal.Terminal.Write(logMessage + "\n", "Logs");
            }

            if (_config.Logging.EnableFileLogging)
            {
                WriteToFile(logMessage);
            }
        }

        private static bool ShouldLog(string level)
        {
            return _config.LogLevel.ToLower() switch
            {
                "debug" => true,
                "info" => level != "Debug",
                "warning" => level is "Warning" or "Error",
                "error" => level == "Error",
                _ => true,
            };
        }

        private static void WriteToFile(string message)
        {
            lock (_lock)
            {
                var logFile = CurrentLogFile;
                var logDir = Path.GetDirectoryName(logFile);

                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir!);
                }

                // Rotate logs if needed
                RotateLogs();

                File.AppendAllText(logFile, message + Environment.NewLine);
            }
        }

        private static void RotateLogs()
        {
            var logDir = Core.GetPath(_config.Logging.LogDirectory);
            var logFiles = Directory
                .GetFiles(logDir, "log_*.txt")
                .OrderByDescending(f => f)
                .ToList();

            // Check file size
            var currentLog = CurrentLogFile;
            if (File.Exists(currentLog))
            {
                var fileInfo = new FileInfo(currentLog);
                if (fileInfo.Length > _config.Logging.MaxLogSize)
                {
                    var newName = Path.Combine(
                        logDir,
                        $"log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt"
                    );
                    File.Move(currentLog, newName);
                    logFiles.Insert(0, newName);
                }
            }

            // Remove old files
            while (logFiles.Count > _config.Logging.MaxLogFiles)
            {
                var oldFile = logFiles.Last();
                try
                {
                    File.Delete(oldFile);
                    logFiles.RemoveAt(logFiles.Count - 1);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deleting old log file: {ex.Message}");
                }
            }
        }
    }
}
