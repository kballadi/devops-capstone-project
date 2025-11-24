using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace LogiTrack.Models;
public class ApplicationUser : IdentityUser
{
    // Password expiration tracking (force password change every 90 days)
    // Default is null; set explicitly when password is changed.
    public DateTime? LastPasswordChangeDate { get; set; }

    // Previous password hashes to prevent reuse (store up to 3 previous hashes)
    public string? PreviousPasswordHashes { get; set; } // Comma-separated list

    // Account security - using IdentityUser's built-in AccessFailedCount and lockout features
    public DateTime? LastLoginDate { get; set; }
    public DateTime? AccountCreatedDate { get; set; } = DateTime.UtcNow;

        /// <summary>
    /// Check if password has expired (90 days since last change)
    /// </summary>
    public bool IsPasswordExpired()
    {
        if (LastPasswordChangeDate == null)
            return false;
        return DateTime.UtcNow.Subtract((DateTime)LastPasswordChangeDate).TotalDays > 90;
    }
}