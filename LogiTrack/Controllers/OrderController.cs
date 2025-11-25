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
    public class OrderController : CachedControllerBase<Order>
    {
        private const string CACHE_KEY_PREFIX = "order_data";

        public OrderController(LogiTrackContext db, IMemoryCache cache) 
            : base(db, cache)
        {
        }

        [HttpGet]
        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> GetOrders(
            [Range(0, int.MaxValue)] int skip = 0,
            [Range(1, MAX_PAGE_SIZE)] int take = DEFAULT_PAGE_SIZE)
        {
            take = Math.Min(take, MAX_PAGE_SIZE);
            string cacheKey = BuildCacheKey(CACHE_KEY_PREFIX, "skip", skip, "take", take);

            // Try to get from cache first
            if (TryGetFromCache(cacheKey, out List<Order>? cachedData))
            {
                return CacheResponse(cachedData, fromCache: true, new { skip, take });
            }

            // Performance: Cache total count separately to avoid N+1 query
            string countCacheKey = BuildCacheKey(CACHE_KEY_PREFIX, "total_count");
            if (!TryGetFromCache(countCacheKey, out int totalCount))
            {
                totalCount = await _db.Orders.CountAsync();
                SetCache(countCacheKey, totalCount, new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(1))); // Short TTL for count
            }

            // Use AsNoTracking for read-only queries + Include to load related items in single query (eager loading)
            var orders = await _db.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .OrderBy(o => o.OrderId)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            SetCache(cacheKey, orders);
            return CacheResponse(orders, fromCache: false, new { skip, take, total = totalCount, count = orders.Count });
        }

        [HttpGet("{id}")]
        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> GetOrder(int id)
        {
            string cacheKey = BuildCacheKey(CACHE_KEY_PREFIX, id);

            // Try to get from cache first
            if (TryGetFromCache(cacheKey, out Order? cachedData))
            {
                return CacheResponse(cachedData, fromCache: true, new { id });
            }

            // AsNoTracking for read-only queries + Include for eager loading relationships
            var order = await _db.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                return NotFound(new { message = $"Order {id} not found" });
            }

            SetCache(cacheKey, order);
            return CacheResponse(order, fromCache: false, new { id });
        }

        [HttpPost]
        public async Task<IActionResult> AddOrder([FromBody] Order order)
        {
            if (order == null)
            {
                return BadRequest("Order is null.");
            }

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            // Invalidate caches (including count cache)
            InvalidateCachePattern(CACHE_KEY_PREFIX);

            return CreatedAtAction(
                nameof(GetOrder), 
                new { id = order.OrderId }, 
                new { source = "database", id = order.OrderId, data = order });
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            // Use FirstOrDefaultAsync for explicit query control instead of FindAsync
            var order = await _db.Orders
                .FirstOrDefaultAsync(o => o.OrderId == id);
            
            if (order == null)
            {
                return NotFound();
            }

            _db.Orders.Remove(order);
            await _db.SaveChangesAsync();

            // Invalidate caches (including count cache)
            InvalidateCachePattern(CACHE_KEY_PREFIX, specificId: id);

            return Ok(new { source = "database", id, data = order });
        }
    }
}