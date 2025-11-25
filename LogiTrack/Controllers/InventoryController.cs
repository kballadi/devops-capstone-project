using LogiTrack.Models;
using LogiTrack.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using System.ComponentModel.DataAnnotations;

namespace LogiTrack.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Manager")]
    public class InventoryController : CachedControllerBase<InventoryItem>
    {
        private const string CACHE_KEY_PREFIX = "inventory";

        public InventoryController(LogiTrackContext db, IMemoryCache cache, IPerformanceProfiler profiler)
            : base(db, cache, profiler)
        {
        }

    // Validation helper method
    private bool ValidateInventoryItem(InventoryItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Name))
            return false;
        if (item.Quantity < 0)
            return false;
        if (string.IsNullOrWhiteSpace(item.Location))
            return false;
        return true;
    }

        // Cache invalidation helper - now uses base class method
        private void InvalidateInventoryCache(int? specificItemId = null)
        {
            InvalidateCachePattern(CACHE_KEY_PREFIX, specificItemId);
        }

    // Controller actions would go here
    /// <summary>
    /// Get paginated list of inventory items with optional filtering
    /// </summary>
    [HttpGet]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetInventoryItems(
        [Range(0, int.MaxValue)] int skip = 0,
        [Range(1, MAX_PAGE_SIZE)] int take = DEFAULT_PAGE_SIZE)
    {
            var stopwatch = _profiler?.StartMeasurement("GetInventoryItems");
            take = Math.Min(take, MAX_PAGE_SIZE); // Cap at max page size

            string cacheKey = BuildCacheKey(CACHE_KEY_PREFIX, "items_skip", skip, "take", take);

            // Try to get from cache first
            if (TryGetFromCache(cacheKey, out List<InventoryItem>? cachedData))
            {
                _profiler?.StopMeasurement(stopwatch, "GetInventoryItems", usedCache: true);
                return CacheResponse(cachedData, fromCache: true, new { skip, take });
        }

        // If not in cache, fetch from database
        var totalCount = await _db.InventoryItems.CountAsync();
        var items = await _db.InventoryItems
            .AsNoTracking()
            .OrderBy(i => i.ItemId)
            .Skip(skip)
            .Take(take)
                .ToListAsync();

            // Store in cache
            SetCache(cacheKey, items);

            _profiler?.StopMeasurement(stopwatch, "GetInventoryItems", usedCache: false);
            return CacheResponse(items, fromCache: false, new { skip, take, total = totalCount, count = items.Count });
    }

    /// <summary>
    /// Search inventory items by name or minimum quantity
    /// </summary>
    [HttpGet("search")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> SearchItems(
        [StringLength(100)] string? name = null,
        [Range(0, int.MaxValue)] int? minQuantity = null,
        [Range(1, MAX_PAGE_SIZE)] int take = DEFAULT_PAGE_SIZE)
    {
            var stopwatch = _profiler?.StartMeasurement("SearchItems");
            take = Math.Min(take, MAX_PAGE_SIZE);

            string cacheKey = BuildCacheKey(CACHE_KEY_PREFIX, "search_name", name ?? "null", "qty", minQuantity ?? 0, "take", take);

            // Try cache first
            if (TryGetFromCache(cacheKey, out List<InventoryItem>? cachedResults))
            {
                _profiler?.StopMeasurement(stopwatch, "SearchItems", usedCache: true);
                return CacheResponse(cachedResults, fromCache: true, null);
            }

        // Build query with filters
        var query = _db.InventoryItems.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(i => i.Name.Contains(name));
        }

        if (minQuantity.HasValue && minQuantity.Value > 0)
        {
            query = query.Where(i => i.Quantity >= minQuantity.Value);
        }

        var results = await query
            .OrderByDescending(i => i.Quantity)
            .Take(take)
                .ToListAsync();

            SetCache(cacheKey, results);

            _profiler?.StopMeasurement(stopwatch, "SearchItems", usedCache: false);
            return CacheResponse(results, fromCache: false, new { count = results.Count });
    }

    /// <summary>
    /// Get lightweight inventory summary (projection for better performance)
    /// </summary>
    [HttpGet("summary")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetInventorySummary(
        [Range(1, MAX_PAGE_SIZE)] int take = DEFAULT_PAGE_SIZE)
    {
            var stopwatch = _profiler?.StartMeasurement("GetInventorySummary");
            take = Math.Min(take, MAX_PAGE_SIZE);

            string cacheKey = BuildCacheKey(CACHE_KEY_PREFIX, "summary_take", take);

            if (TryGetFromCache(cacheKey, out dynamic? cachedSummary))
            {
                _profiler?.StopMeasurement(stopwatch, "GetInventorySummary", usedCache: true);
                return CacheResponse(cachedSummary, true, null);
            }

        // Projection: only fetch needed columns for better performance
        var summary = await _db.InventoryItems
            .AsNoTracking()
            .Select(i => new
            {
                i.ItemId,
                i.Name,
                i.Quantity,
                i.Location
            })
            .OrderBy(i => i.ItemId)
            .Take(take)
                .ToListAsync();

            SetCache(cacheKey, summary);

            _profiler?.StopMeasurement(stopwatch, "GetInventorySummary", usedCache: false);
            return CacheResponse(summary, fromCache: false, new { count = summary.Count });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetInventoryItem(int id)
    {
            string cacheKey = BuildCacheKey(CACHE_KEY_PREFIX, "item", id);
            var stopwatch = _profiler?.StartMeasurement($"GetInventoryItem({id})");

            // Try to get from cache first
            if (TryGetFromCache(cacheKey, out InventoryItem? cachedItem))
            {
                _profiler?.StopMeasurement(stopwatch, $"GetInventoryItem({id})", usedCache: true);
                return CacheResponse(cachedItem, fromCache: true, null);
            }

        // Fetch from database if not cached using AsNoTracking for read-only queries
        var item = await _db.InventoryItems
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.ItemId == id);
            if (item == null)
            {
                _profiler?.StopMeasurement(stopwatch, $"GetInventoryItem({id})", usedCache: false);
                return NotFound();
            }

            // Cache the individual item
            SetCache(cacheKey, item);

            _profiler?.StopMeasurement(stopwatch, $"GetInventoryItem({id})", usedCache: false);
            return CacheResponse(item, fromCache: false, null);
    }

    [HttpPost]
    public async Task<IActionResult> AddInventoryItem([FromBody] InventoryItem item)
    {
        if (item == null)
        {
            return BadRequest(new { message = "Inventory item is null." });
        }

        // Validate inventory item before processing
        if (!ValidateInventoryItem(item))
        {
            return BadRequest(new
            {
                message = "Invalid inventory item",
                errors = new
                {
                    name = "Name cannot be empty",
                    quantity = "Quantity cannot be negative",
                    location = "Location cannot be empty"
                }
            });
        }

        _db.InventoryItems.Add(item);
        await _db.SaveChangesAsync();

        // Batch invalidate caches (replaces individual remove calls)
        InvalidateInventoryCache();

        return CreatedAtAction(nameof(GetInventoryItems), new { id = item.ItemId }, item);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateInventoryItem(int id, [FromBody] InventoryItem updatedItem)
    {
        if (updatedItem == null)
        {
            return BadRequest(new { message = "Inventory item is null." });
        }

        // Validate before processing
        if (!ValidateInventoryItem(updatedItem))
        {
            return BadRequest(new
            {
                message = "Invalid inventory item",
                errors = new
                {
                    name = "Name cannot be empty",
                    quantity = "Quantity cannot be negative",
                    location = "Location cannot be empty"
                }
            });
        }

        // Use FirstOrDefaultAsync for better query control and consistency
        var item = await _db.InventoryItems
            .FirstOrDefaultAsync(i => i.ItemId == id);
        if (item == null)
        {
            return NotFound(new { message = $"Inventory item with ID {id} not found." });
        }

        // Update the item
        item.Name = updatedItem.Name;
        item.Quantity = updatedItem.Quantity;
        item.Location = updatedItem.Location;

        _db.InventoryItems.Update(item);
        await _db.SaveChangesAsync();

        // Batch invalidate caches
        InvalidateInventoryCache(id);

        return Ok(item);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteInventoryItem(int id)
    {
        // Use FirstOrDefaultAsync for explicit query control and predictable behavior
        var item = await _db.InventoryItems
            .FirstOrDefaultAsync(i => i.ItemId == id);
        if (item == null)
        {
            return NotFound(new { message = $"Inventory item with ID {id} not found." });
        }

        _db.InventoryItems.Remove(item);
        await _db.SaveChangesAsync();

        // Batch invalidate caches
        InvalidateInventoryCache(id);

        return NoContent();
    }

        [HttpPost("clear-cache")]
        [Authorize(Roles = "Admin")]
        public IActionResult ClearCache()
        {
            InvalidateCachePattern(CACHE_KEY_PREFIX);
            return Ok(new { message = "Cache cleared successfully." });
    }

    [HttpGet("performance/stats")]
    [Authorize(Roles = "Admin")]
    public IActionResult GetPerformanceStats()
    {
        var stats = _profiler.GetAllStats();
        return Ok(stats);
    }

    [HttpGet("performance/stats/{operationName}")]
    [Authorize(Roles = "Admin")]
    public IActionResult GetOperationStats(string operationName)
    {
        var stats = _profiler.GetStats(operationName);
        if (stats == null)
            return NotFound(new { message = $"No stats found for operation: {operationName}" });
        return Ok(stats);
    }

    [HttpPost("performance/export")]
    [Authorize(Roles = "Admin")]
    public IActionResult ExportPerformanceMetrics()
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var filePath = Path.Combine(Path.GetTempPath(), $"performance_metrics_{timestamp}.csv");
        _profiler.ExportToCsv(filePath);
        return Ok(new { message = "Metrics exported", filePath = filePath });
    }

    [HttpPost("performance/clear")]
    [Authorize(Roles = "Admin")]
    public IActionResult ClearPerformanceMetrics()
    {
        _profiler.ClearMetrics();
            return Ok(new { message = "Performance metrics cleared." });
        }
    }
}