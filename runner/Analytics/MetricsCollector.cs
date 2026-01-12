using System.Collections.Concurrent;
using System.Text.Json;

namespace KodeRunner.Analytics
{
    public class MetricsCollector
    {
        private readonly ConcurrentQueue<ExecutionMetric> _metrics = new();
        private readonly Timer _flushTimer;
        private readonly string _metricsPath;

        public MetricsCollector()
        {
            _metricsPath = Path.Combine(Core.GetPath(Core.LogDir), "metrics");
            Directory.CreateDirectory(_metricsPath);
            
            // Flush metrics every 5 minutes
            _flushTimer = new Timer(FlushMetrics, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        public void RecordExecution(string language, string projectName, TimeSpan duration, bool success, long memoryUsed = 0)
        {
            var metric = new ExecutionMetric
            {
                Timestamp = DateTime.UtcNow,
                Language = language,
                ProjectName = projectName,
                Duration = duration,
                Success = success,
                MemoryUsedMB = memoryUsed / (1024 * 1024)
            };

            _metrics.Enqueue(metric);
            
            // Update terminal display
            Terminal.Terminal.Write($"Execution: {language} - {(success ? "✓" : "✗")} - {duration.TotalSeconds:F2}s\n", "Logs");
        }

        public ExecutionStats GetStats(DateTime? since = null)
        {
            var cutoff = since ?? DateTime.UtcNow.AddDays(-7);
            var recentMetrics = _metrics.Where(m => m.Timestamp >= cutoff).ToList();

            return new ExecutionStats
            {
                TotalExecutions = recentMetrics.Count,
                SuccessfulExecutions = recentMetrics.Count(m => m.Success),
                AverageExecutionTime = recentMetrics.Any() ? 
                    TimeSpan.FromMilliseconds(recentMetrics.Average(m => m.Duration.TotalMilliseconds)) : 
                    TimeSpan.Zero,
                TopLanguages = recentMetrics
                    .GroupBy(m => m.Language)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .ToDictionary(g => g.Key, g => g.Count()),
                AverageMemoryUsage = recentMetrics.Any() ? recentMetrics.Average(m => m.MemoryUsedMB) : 0
            };
        }

        private void FlushMetrics(object state)
        {
            var metricsToFlush = new List<ExecutionMetric>();
            while (_metrics.TryDequeue(out var metric))
            {
                metricsToFlush.Add(metric);
            }

            if (!metricsToFlush.Any()) return;

            var fileName = $"metrics_{DateTime.UtcNow:yyyy-MM-dd}.json";
            var filePath = Path.Combine(_metricsPath, fileName);
            
            try
            {
                var json = JsonSerializer.Serialize(metricsToFlush, new JsonSerializerOptions { WriteIndented = true });
                File.AppendAllText(filePath, json + Environment.NewLine);
                Logger.Log($"Flushed {metricsToFlush.Count} metrics to {fileName}");
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to flush metrics: {ex.Message}", "Error");
                // Re-queue metrics if write failed
                foreach (var metric in metricsToFlush)
                {
                    _metrics.Enqueue(metric);
                }
            }
        }
    }

    public class ExecutionMetric
    {
        public DateTime Timestamp { get; set; }
        public string Language { get; set; }
        public string ProjectName { get; set; }
        public TimeSpan Duration { get; set; }
        public bool Success { get; set; }
        public long MemoryUsedMB { get; set; }
    }

    public class ExecutionStats
    {
        public int TotalExecutions { get; set; }
        public int SuccessfulExecutions { get; set; }
        public TimeSpan AverageExecutionTime { get; set; }
        public Dictionary<string, int> TopLanguages { get; set; } = new();
        public double AverageMemoryUsage { get; set; }
    }
}
