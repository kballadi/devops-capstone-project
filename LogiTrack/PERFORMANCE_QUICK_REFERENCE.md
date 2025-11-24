# Performance Profiling Quick Reference

## Overview
Built-in performance profiling and benchmarking tools to measure cache effectiveness in LogiTrack.

---

## Key Files

| File | Purpose |
|------|---------|
| `Services/PerformanceProfiler.cs` | Core profiling service - tracks all metrics |
| `Testing/PerformanceBenchmark.cs` | Benchmark test harness |
| `Controllers/InventoryController.cs` | Profiling endpoints + instrumented methods |
| `PERFORMANCE_PROFILING.md` | Full documentation |

---

## Quick Start: Performance Profiling Endpoints

### 1. **View All Performance Stats**
```bash
curl -X GET "https://localhost:5001/api/inventory/performance/stats" \
  -H "Authorization: Bearer {JWT_TOKEN}"
```

**Output:** JSON with all operation metrics (response times, cache hit rates, memory usage)

### 2. **View Stats for Specific Operation**
```bash
curl -X GET "https://localhost:5001/api/inventory/performance/stats/GetInventoryItems" \
  -H "Authorization: Bearer {JWT_TOKEN}"
```

### 3. **Export Metrics to CSV**
```bash
curl -X POST "https://localhost:5001/api/inventory/performance/export" \
  -H "Authorization: Bearer {JWT_TOKEN}"
```

Output: CSV file in temp folder with all measurements

### 4. **Clear All Metrics** (for fresh benchmarks)
```bash
curl -X POST "https://localhost:5001/api/inventory/performance/clear" \
  -H "Authorization: Bearer {JWT_TOKEN}"
```

### 5. **Clear Cache** (to test cold start)
```bash
curl -X POST "https://localhost:5001/api/inventory/clear-cache" \
  -H "Authorization: Bearer {JWT_TOKEN}"
```

---

## Quick Benchmark: PowerShell One-Liner

```powershell
# Measure single endpoint
$base = "https://localhost:5001"
$token = "YOUR_TOKEN"
$h = @{"Authorization" = "Bearer $token"}

# Cold cache (after clear)
(Invoke-WebRequest "$base/api/inventory/performance/clear" -Method Post -Headers $h -ErrorAction Ignore) | Out-Null
$s = Measure-Object -ScriptBlock { Invoke-WebRequest "$base/api/inventory" -Headers $h } -Seconds
Write-Host "Cold: $($s.TotalSeconds * 1000)ms"

# Warm cache (cached)
$s = Measure-Object -ScriptBlock { Invoke-WebRequest "$base/api/inventory" -Headers $h } -Seconds
Write-Host "Warm: $($s.TotalSeconds * 1000)ms"
```

---

## Expected Results

| Scenario | Response Time | Source |
|----------|---------------|--------|
| **Cold Cache (first request)** | 100-300ms | Database |
| **Warm Cache (subsequent)** | 5-15ms | Memory |
| **Speedup Factor** | **10-60x** | Cache vs DB |
| **Cache Hit Rate** | 80-95% | After warmup |

---

## Profiling Points in Code

Automatic profiling added to:

```csharp
// GetInventoryItems - all GET requests tracked
[HttpGet]
public async Task<IActionResult> GetInventoryItems()
{
    var stopwatch = _profiler.StartMeasurement("GetInventoryItems");
    // ... code ...
    _profiler.StopMeasurement(stopwatch, "GetInventoryItems", usedCache: true/false);
}

// GetInventoryItem(id) - individual item tracking
[HttpGet("{id}")]
public async Task<IActionResult> GetInventoryItem(int id)
{
    var stopwatch = _profiler.StartMeasurement($"GetInventoryItem({id})");
    // ... code ...
    _profiler.StopMeasurement(stopwatch, $"GetInventoryItem({id})", usedCache: true/false);
}
```

---

## Real-World Example: Full Benchmark

### Step 1: Clear Everything
```bash
curl -X POST "https://localhost:5001/api/inventory/performance/clear" \
  -H "Authorization: Bearer $TOKEN"
curl -X POST "https://localhost:5001/api/inventory/clear-cache" \
  -H "Authorization: Bearer $TOKEN"
```

### Step 2: Make 10 Requests (populate cache)
```bash
for i in {1..10}; do
  curl -s "https://localhost:5001/api/inventory" \
    -H "Authorization: Bearer $TOKEN" > /dev/null
  echo "Request $i done"
done
```

### Step 3: View Results
```bash
curl -s "https://localhost:5001/api/inventory/performance/stats" \
  -H "Authorization: Bearer $TOKEN" | jq .
```

### Expected Output:
```json
{
  "GetInventoryItems": {
    "operationName": "GetInventoryItems",
    "totalRequests": 10,
    "cacheHits": 9,           ← Cache hits after first request
    "cacheMisses": 1,
    "averageResponseTime": 8.5,  ← Average warmup response
    "minResponseTime": 5,
    "maxResponseTime": 250,       ← First request (cold cache)
    "cacheHitRate": 90.0         ← 90% cache hit rate!
  }
}
```

---

## Performance Analysis Checklist

- [ ] **Cache Hit Rate > 70%** - Caching is effective
- [ ] **Average Response < 20ms** - Good performance
- [ ] **First Request 100-300ms** - Expected (cold cache/DB)
- [ ] **Subsequent Requests 5-15ms** - Cache working (10-60x faster)
- [ ] **Memory Usage Stable** - No leaks
- [ ] **Request Volume High** - Multiple requests logged

---

## Troubleshooting

| Issue | Cause | Fix |
|-------|-------|-----|
| Low cache hit rate | Short cache duration | Increase `SetAbsoluteExpiration` |
| Slow warm cache | Serialization overhead | Use faster JSON serializer |
| Growing memory | Cache not expiring | Verify sliding expiration set |
| No metrics recorded | Endpoint not called | Verify request goes to profiled endpoint |

---

## Monitoring Commands

```bash
# Watch stats in real-time (every 5 sec)
watch -n 5 'curl -s "https://localhost:5001/api/inventory/performance/stats" -H "Authorization: Bearer $TOKEN" | jq .'

# Export to CSV then analyze
curl -X POST "https://localhost:5001/api/inventory/performance/export" -H "Authorization: Bearer $TOKEN"
# Open CSV in Excel/Google Sheets for analysis
```

---

## Key Metrics Explained

- **CacheHits** - Number of requests served from cache
- **CacheMisses** - Number of requests requiring DB query
- **CacheHitRate** - (CacheHits / TotalRequests) × 100%
- **AverageResponseTime** - Mean response time across all requests
- **PerformanceImprovement** - ((ColdTime - WarmTime) / ColdTime) × 100%
- **MemoryAverageMB** - Average memory used per operation

---

## Next Steps

1. Run baseline benchmark (step 1-3 above)
2. Identify slow operations
3. Increase cache duration for those operations
4. Re-run benchmark to measure improvement
5. Set up monitoring for production metrics
6. Consider distributed cache (Redis) for scale-out

---

## See Also

- `PERFORMANCE_PROFILING.md` - Full documentation
- `EF_OPTIMIZATION.md` - EF Core query optimization guide
- `SECURITY.md` - Security features
