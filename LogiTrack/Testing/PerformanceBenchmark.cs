using System.Diagnostics;

namespace LogiTrack.Testing
{
    /// <summary>
    /// Benchmark utility for comparing query performance before and after caching
    /// </summary>
    public class PerformanceBenchmark
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<PerformanceBenchmark> _logger;

        public PerformanceBenchmark(HttpClient httpClient, ILogger<PerformanceBenchmark> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        /// <summary>
        /// Run a benchmark test comparing cold cache vs warm cache performance
        /// </summary>
        public async Task<BenchmarkResult> RunCacheBenchmarkAsync(
            string endpoint,
            int numberOfWarmupRequests = 1,
            int numberOfTestRequests = 10)
        {
            _logger.LogInformation("Starting performance benchmark for {Endpoint}", endpoint);

            var result = new BenchmarkResult { Endpoint = endpoint };

            // Phase 1: Clear cache (if available)
            _logger.LogInformation("Phase 1: Clearing cache...");
            try
            {
                await _httpClient.PostAsync($"/api/inventory/performance/clear", new StringContent(""));
            }
            catch { /* Ignore if clear endpoint not available */ }

            // Phase 2: Warmup requests (populate cache)
            _logger.LogInformation("Phase 2: Running {Count} warmup requests...", numberOfWarmupRequests);
            for (int i = 0; i < numberOfWarmupRequests; i++)
            {
                try
                {
                    var response = await _httpClient.GetAsync(endpoint);
                    response.EnsureSuccessStatusCode();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Warmup request failed");
                }
                await Task.Delay(100); // Small delay between requests
            }

            // Phase 3: Cold cache requests (first request after clear)
            _logger.LogInformation("Phase 3: Measuring cold cache performance...");
            var coldCacheTimes = new List<long>();

            try
            {
                await _httpClient.PostAsync($"/api/inventory/performance/clear", new StringContent(""));
            }
            catch { /* Ignore */ }

            var stopwatch = Stopwatch.StartNew();
            var response1 = await _httpClient.GetAsync(endpoint);
            stopwatch.Stop();
            coldCacheTimes.Add(stopwatch.ElapsedMilliseconds);
            response1.EnsureSuccessStatusCode();

            _logger.LogInformation("Cold cache response time: {Time}ms", stopwatch.ElapsedMilliseconds);

            // Phase 4: Warm cache requests (cached responses)
            _logger.LogInformation("Phase 4: Measuring warm cache performance ({Count} requests)...", numberOfTestRequests);
            var warmCacheTimes = new List<long>();

            for (int i = 0; i < numberOfTestRequests; i++)
            {
                stopwatch = Stopwatch.StartNew();
                var response = await _httpClient.GetAsync(endpoint);
                stopwatch.Stop();
                warmCacheTimes.Add(stopwatch.ElapsedMilliseconds);
                response.EnsureSuccessStatusCode();

                await Task.Delay(50); // Small delay between requests
            }

            // Calculate statistics
            result.ColdCacheTime = coldCacheTimes.First();
            result.WarmCacheAverageTime = warmCacheTimes.Average();
            result.WarmCacheMinTime = warmCacheTimes.Min();
            result.WarmCacheMaxTime = warmCacheTimes.Max();
            result.PerformanceImprovement = result.ColdCacheTime > 0 
                ? ((result.ColdCacheTime - result.WarmCacheAverageTime) / result.ColdCacheTime) * 100 
                : 0;

            _logger.LogInformation("Benchmark completed for {Endpoint}", endpoint);

            return result;
        }

        /// <summary>
        /// Run multiple concurrent requests to measure cache hit rate
        /// </summary>
        public async Task<ConcurrencyBenchmarkResult> RunConcurrencyBenchmarkAsync(
            string endpoint,
            int numberOfConcurrentRequests = 10,
            int numberOfIterations = 5)
        {
            _logger.LogInformation(
                "Starting concurrency benchmark: {Requests} concurrent requests x {Iterations} iterations",
                numberOfConcurrentRequests,
                numberOfIterations);

            var result = new ConcurrencyBenchmarkResult { Endpoint = endpoint };
            var allTimes = new List<long>();

            for (int iteration = 0; iteration < numberOfIterations; iteration++)
            {
                var tasks = new List<Task<long>>();

                for (int i = 0; i < numberOfConcurrentRequests; i++)
                {
                    tasks.Add(MeasureRequestAsync(endpoint));
                }

                var times = await Task.WhenAll(tasks);
                allTimes.AddRange(times);
            }

            result.TotalRequests = allTimes.Count;
            result.AverageResponseTime = allTimes.Average();
            result.MinResponseTime = allTimes.Min();
            result.MaxResponseTime = allTimes.Max();
            result.TotalTimeMs = allTimes.Sum();
            result.RequestsPerSecond = (result.TotalRequests / (result.TotalTimeMs / 1000.0));

            _logger.LogInformation("Concurrency benchmark completed");

            return result;
        }

        private async Task<long> MeasureRequestAsync(string endpoint)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var response = await _httpClient.GetAsync(endpoint);
                stopwatch.Stop();
                response.EnsureSuccessStatusCode();
                return stopwatch.ElapsedMilliseconds;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Request failed");
                stopwatch.Stop();
                return stopwatch.ElapsedMilliseconds;
            }
        }
    }

    /// <summary>
    /// Results from cache performance benchmark
    /// </summary>
    public class BenchmarkResult
    {
        public string Endpoint { get; set; } = string.Empty;
        public long ColdCacheTime { get; set; }
        public double WarmCacheAverageTime { get; set; }
        public long WarmCacheMinTime { get; set; }
        public long WarmCacheMaxTime { get; set; }
        public double PerformanceImprovement { get; set; }

        public override string ToString()
        {
            return $@"
========== CACHE PERFORMANCE BENCHMARK ==========
Endpoint: {Endpoint}
Cold Cache Response Time: {ColdCacheTime}ms
Warm Cache Average Time: {WarmCacheAverageTime:F2}ms
Warm Cache Min Time: {WarmCacheMinTime}ms
Warm Cache Max Time: {WarmCacheMaxTime}ms
Performance Improvement: {PerformanceImprovement:F2}%
Speedup Factor: {(ColdCacheTime / WarmCacheAverageTime):F2}x faster
==================================================";
        }
    }

    /// <summary>
    /// Results from concurrency benchmark
    /// </summary>
    public class ConcurrencyBenchmarkResult
    {
        public string Endpoint { get; set; } = string.Empty;
        public int TotalRequests { get; set; }
        public double AverageResponseTime { get; set; }
        public long MinResponseTime { get; set; }
        public long MaxResponseTime { get; set; }
        public long TotalTimeMs { get; set; }
        public double RequestsPerSecond { get; set; }

        public override string ToString()
        {
            return $@"
========== CONCURRENCY BENCHMARK RESULTS ==========
Endpoint: {Endpoint}
Total Requests: {TotalRequests}
Average Response Time: {AverageResponseTime:F2}ms
Min Response Time: {MinResponseTime}ms
Max Response Time: {MaxResponseTime}ms
Total Time: {TotalTimeMs}ms
Requests Per Second: {RequestsPerSecond:F2}
====================================================";
        }
    }
}
