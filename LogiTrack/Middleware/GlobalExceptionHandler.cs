using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LogiTrack.Middleware
{
    /// <summary>
    /// Global exception handler that provides consistent error responses
    /// and prevents stack trace leaks in production
    /// </summary>
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            _logger.LogError(
                exception,
                "Unhandled exception occurred. Path: {Path}, Method: {Method}",
                httpContext.Request.Path,
                httpContext.Request.Method);

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An error occurred while processing your request",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Instance = httpContext.Request.Path
            };

            // Only include exception details in development
            var environment = httpContext.RequestServices.GetService<IHostEnvironment>();
            if (environment?.IsDevelopment() == true)
            {
                problemDetails.Detail = exception.Message;
                problemDetails.Extensions["stackTrace"] = exception.StackTrace;
            }
            else
            {
                problemDetails.Detail = "An unexpected error occurred. Please try again later.";
            }

            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

            return true; // Exception handled
        }
    }
}
