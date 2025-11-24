using System.Collections.Concurrent;

namespace LogiTrack.Middleware
{
    /// <summary>
    /// Simple in-memory rate limiting middleware for registration and login endpoints.
    /// Tracks requests per IP address and prevents spam.
    /// </summary>
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RateLimitingMiddleware> _logger;
        private static readonly ConcurrentDictionary<string, (int count, DateTime resetTime)> RequestCounts = new();

        public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? string.Empty;

            // Apply rate limiting only to auth endpoints
            if (path.Contains("/api/auth/register") || path.Contains("/api/auth/login"))
            {
                var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var key = $"{clientIp}:{path}";
                var now = DateTime.UtcNow;

                if (RequestCounts.TryGetValue(key, out var record))
                {
                    if (now < record.resetTime)
                    {
                        if (record.count >= 5)
                        {
                            _logger.LogWarning("Rate limit exceeded for IP {ClientIp} on {Path}", clientIp, path);
                            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                            await context.Response.WriteAsJsonAsync(new { message = "Too many requests. Please try again later." });
                            return;
                        }

                        RequestCounts.TryUpdate(key, (record.count + 1, record.resetTime), record);
                    }
                    else
                    {
                        // Reset counter after 1 minute
                        RequestCounts.TryUpdate(key, (1, now.AddMinutes(1)), record);
                    }
                }
                else
                {
                    RequestCounts.TryAdd(key, (1, now.AddMinutes(1)));
                }
            }

            await _next(context);
        }
    }
}
