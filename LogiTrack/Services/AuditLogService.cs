using System;

namespace LogiTrack.Services
{
    public interface IAuditLogService
    {
        Task LogRegistrationAsync(string userId, string email);
        Task LogLoginAsync(string userId, string email);
        Task LogFailedLoginAsync(string email, string reason);
        Task LogFailedRegistrationAsync(string email, string reason);
    }

    public class AuditLogService : IAuditLogService
    {
        private readonly ILogger<AuditLogService> _logger;

        public AuditLogService(ILogger<AuditLogService> logger)
        {
            _logger = logger;
        }

        public async Task LogRegistrationAsync(string userId, string email)
        {
            _logger.LogInformation("AUDIT: User registered. UserId={UserId}, Email={Email}, Timestamp={Timestamp}",
                userId, email, DateTime.UtcNow);
            // TODO: Persist to database or external audit log
            await Task.CompletedTask;
        }

        public async Task LogLoginAsync(string userId, string email)
        {
            _logger.LogInformation("AUDIT: User logged in. UserId={UserId}, Email={Email}, Timestamp={Timestamp}",
                userId, email, DateTime.UtcNow);
            await Task.CompletedTask;
        }

        public async Task LogFailedLoginAsync(string email, string reason)
        {
            _logger.LogWarning("AUDIT: Failed login attempt. Email={Email}, Reason={Reason}, Timestamp={Timestamp}",
                email, reason, DateTime.UtcNow);
            await Task.CompletedTask;
        }

        public async Task LogFailedRegistrationAsync(string email, string reason)
        {
            _logger.LogWarning("AUDIT: Failed registration attempt. Email={Email}, Reason={Reason}, Timestamp={Timestamp}",
                email, reason, DateTime.UtcNow);
            await Task.CompletedTask;
        }
    }
}
