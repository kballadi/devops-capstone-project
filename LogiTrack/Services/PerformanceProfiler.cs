using System.Diagnostics;

namespace LogiTrack.Services
{
    /// <summary>
    /// Performance metric for a single operation
    /// </summary>
    public class PerformanceMetric
    {
        public string OperationName { get; set; } = string.Empty;
        public long ElapsedMilliseconds { get; set; }
        public bool UsedCache { get; set; }
        public DateTime Timestamp { get; set; }
        public long MemoryUsedBytes { get; set; }
    }

    /// <summary>
    /// Aggregated performance statistics
    /// </summary>
    public class PerformanceStats
    {
        public string OperationName { get; set; } = string.Empty;
        public int TotalRequests { get; set; }
        public int CacheHits { get; set; }
        public int CacheMisses { get; set; }
        public double AverageResponseTime { get; set; }
        public long MinResponseTime { get; set; }
        public long MaxResponseTime { get; set; }
        public double CacheHitRate => TotalRequests > 0 ? (CacheHits * 100.0) / TotalRequests : 0;
        public double MemoryAverageMB { get; set; }

        public override string ToString()
        {
            return $@"
Operation: {OperationName}
Total Requests: {TotalRequests}
Cache Hits: {CacheHits} ({CacheHitRate:F2}%)
Cache Misses: {CacheMisses}
Avg Response Time: {AverageResponseTime:F2}ms
Min Response Time: {MinResponseTime}ms
Max Response Time: {MaxResponseTime}ms
Avg Memory: {MemoryAverageMB:F2}MB
";
        }
    }

    /// <summary>
    /// Performance profiling service for measuring query performance
    /// </summary>
    public interface IPerformanceProfiler
    {
        /// <summary>
        /// Start a performance measurement
        /// </summary>
        Stopwatch StartMeasurement(string operationName);

        /// <summary>
        /// Stop measurement and record the metric
        /// </summary>
        void StopMeasurement(Stopwatch stopwatch, string operationName, bool usedCache = false);

        /// <summary>
        /// Get statistics for a specific operation
        /// </summary>
        PerformanceStats? GetStats(string operationName);

        /// <summary>
        /// Get all recorded metrics
        /// </summary>
        List<PerformanceMetric> GetAllMetrics();

        /// <summary>
        /// Get statistics for all operations
        /// </summary>
        Dictionary<string, PerformanceStats> GetAllStats();

        /// <summary>
        /// Clear all metrics (useful for benchmarking sessions)
        /// </summary>
        void ClearMetrics();

        /// <summary>
        /// Export metrics to console
        /// </summary>
        void ExportToConsole();

        /// <summary>
        /// Export metrics to CSV
        /// </summary>
        void ExportToCsv(string filePath);
    }

    /// <summary>
    /// Implementation of performance profiling service
    /// </summary>
    public class PerformanceProfiler : IPerformanceProfiler
    {
        private readonly List<PerformanceMetric> _metrics = new();
        private readonly ILogger<PerformanceProfiler> _logger;
        private readonly object _lock = new object();

        public PerformanceProfiler(ILogger<PerformanceProfiler> logger)
        {
            _logger = logger;
        }

        public Stopwatch StartMeasurement(string operationName)
        {
            var stopwatch = Stopwatch.StartNew();
            return stopwatch;
        }

        public void StopMeasurement(Stopwatch stopwatch, string operationName, bool usedCache = false)
        {
            stopwatch.Stop();
            var memoryUsed = GC.GetTotalMemory(false);

            var metric = new PerformanceMetric
            {
                OperationName = operationName,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                UsedCache = usedCache,
                Timestamp = DateTime.UtcNow,
                MemoryUsedBytes = memoryUsed
            };

            lock (_lock)
            {
                _metrics.Add(metric);
            }

            _logger.LogInformation(
                "PERF: {OperationName} - {Time}ms ({Source})",
                operationName,
                stopwatch.ElapsedMilliseconds,
                usedCache ? "Cache Hit" : "DB Query"
            );
        }

        public PerformanceStats? GetStats(string operationName)
        {
            lock (_lock)
            {
                var metrics = _metrics.Where(m => m.OperationName == operationName).ToList();

                if (metrics.Count == 0)
                    return null;

                return new PerformanceStats
                {
                    OperationName = operationName,
                    TotalRequests = metrics.Count,
                    CacheHits = metrics.Count(m => m.UsedCache),
                    CacheMisses = metrics.Count(m => !m.UsedCache),
                    AverageResponseTime = metrics.Average(m => m.ElapsedMilliseconds),
                    MinResponseTime = metrics.Min(m => m.ElapsedMilliseconds),
                    MaxResponseTime = metrics.Max(m => m.ElapsedMilliseconds),
                    MemoryAverageMB = metrics.Average(m => m.MemoryUsedBytes) / (1024.0 * 1024.0)
                };
            }
        }

        public List<PerformanceMetric> GetAllMetrics()
        {
            lock (_lock)
            {
                return new List<PerformanceMetric>(_metrics);
            }
        }

        public Dictionary<string, PerformanceStats> GetAllStats()
        {
            lock (_lock)
            {
                var operations = _metrics.Select(m => m.OperationName).Distinct();
                var stats = new Dictionary<string, PerformanceStats>();

                foreach (var operation in operations)
                {
                    var operationStats = GetStats(operation);
                    if (operationStats != null)
                        stats[operation] = operationStats;
                }

                return stats;
            }
        }

        public void ClearMetrics()
        {
            lock (_lock)
            {
                _metrics.Clear();
                _logger.LogInformation("Performance metrics cleared");
            }
        }

        public void ExportToConsole()
        {
            lock (_lock)
            {
                var stats = GetAllStats();

                Console.WriteLine("\n========== PERFORMANCE PROFILING REPORT ==========\n");

                foreach (var stat in stats.Values)
                {
                    Console.WriteLine(stat.ToString());
                }

                // Summary
                var totalRequests = stats.Values.Sum(s => s.TotalRequests);
                var totalCacheHits = stats.Values.Sum(s => s.CacheHits);
                var overallCacheHitRate = totalRequests > 0 ? (totalCacheHits * 100.0) / totalRequests : 0;
                var avgResponseTime = stats.Values.Average(s => s.AverageResponseTime);

                Console.WriteLine("\n========== OVERALL SUMMARY ==========");
                Console.WriteLine($"Total Requests: {totalRequests}");
                Console.WriteLine($"Total Cache Hits: {totalCacheHits}");
                Console.WriteLine($"Overall Cache Hit Rate: {overallCacheHitRate:F2}%");
                Console.WriteLine($"Average Response Time: {avgResponseTime:F2}ms");
                Console.WriteLine("====================================\n");
            }
        }

        public void ExportToCsv(string filePath)
        {
            lock (_lock)
            {
                try
                {
                    using (var writer = new StreamWriter(filePath))
                    {
                        // Header
                        writer.WriteLine("Timestamp,Operation,ElapsedMs,UsedCache,MemoryMB");

                        // Data rows
                        foreach (var metric in _metrics)
                        {
                            var memoryMB = metric.MemoryUsedBytes / (1024.0 * 1024.0);
                            writer.WriteLine(
                                $"{metric.Timestamp:O},{metric.OperationName},{metric.ElapsedMilliseconds},{metric.UsedCache},{memoryMB:F2}"
                            );
                        }
                    }

                    _logger.LogInformation("Performance metrics exported to {FilePath}", filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error exporting metrics to CSV");
                }
            }
        }
    }
}
