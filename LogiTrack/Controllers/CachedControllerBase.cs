using LogiTrack.Models;
using LogiTrack.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace LogiTrack.Controllers
{
    /// <summary>
    /// Base controller providing shared caching functionality for entity controllers
    /// </summary>
    public abstract class CachedControllerBase<TEntity> : ControllerBase where TEntity : class
    {
        protected readonly LogiTrackContext _db;
        protected readonly IMemoryCache _cache;
        protected readonly IPerformanceProfiler? _profiler;

        // Shared constants
        protected const int CACHE_DURATION_MINUTES = 5;
        protected const int DEFAULT_PAGE_SIZE = 50;
        protected const int MAX_PAGE_SIZE = 100;

        // Shared cache options
        protected static readonly MemoryCacheEntryOptions DefaultCacheOptions =
            new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(CACHE_DURATION_MINUTES))
                .SetSlidingExpiration(TimeSpan.FromMinutes(1));

        protected CachedControllerBase(
            LogiTrackContext db, 
            IMemoryCache cache, 
            IPerformanceProfiler? profiler = null)
        {
            _db = db;
            _cache = cache;
            _profiler = profiler;
        }

        /// <summary>
        /// Build a cache key from a prefix and parts
        /// </summary>
        protected string BuildCacheKey(string prefix, params object[] parts)
        {
            if (parts.Length == 0)
                return prefix;
            
            return $"{prefix}_{string.Join("_", parts)}";
        }

        /// <summary>
        /// Try to get a value from cache
        /// </summary>
        protected bool TryGetFromCache<T>(string cacheKey, out T? value)
        {
            return _cache.TryGetValue(cacheKey, out value);
        }

        /// <summary>
        /// Set a value in cache with optional custom options
        /// </summary>
        protected void SetCache<T>(string cacheKey, T value, MemoryCacheEntryOptions? options = null)
        {
            _cache.Set(cacheKey, value, options ?? DefaultCacheOptions);
        }

        /// <summary>
        /// Invalidate cache entries matching a pattern
        /// </summary>
        protected void InvalidateCachePattern(string baseKey, int? specificId = null, int maxPages = 10)
        {
            // Remove base cache key
            _cache.Remove(baseKey);

            // Invalidate paginated caches
            for (int i = 0; i < maxPages; i++)
            {
                _cache.Remove($"{baseKey}_skip_{i * DEFAULT_PAGE_SIZE}_take_{DEFAULT_PAGE_SIZE}");
            }

            // Invalidate specific item cache if provided
            if (specificId.HasValue)
            {
                _cache.Remove($"{baseKey}_{specificId}");
            }
        }

        /// <summary>
        /// Create a standardized cache response
        /// </summary>
        protected IActionResult CacheResponse<T>(T data, bool fromCache, object? metadata = null)
        {
            var response = new Dictionary<string, object>
            {
                ["source"] = fromCache ? "cache" : "database",
                ["data"] = data!
            };

            if (metadata != null)
            {
                foreach (var prop in metadata.GetType().GetProperties())
                {
                    response[prop.Name] = prop.GetValue(metadata)!;
                }
            }

            return Ok(response);
        }
    }
}
