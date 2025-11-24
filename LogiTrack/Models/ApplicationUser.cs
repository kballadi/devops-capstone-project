using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

public class ApplicationUser : IdentityUser
{
    // Password expiration tracking (force password change every 90 days)
    public DateTime? LastPasswordChangeDate { get; set; } = DateTime.UtcNow;

    // Previous password hashes to prevent reuse (store up to 3 previous hashes)
    public string? PreviousPasswordHashes { get; set; } // Comma-separated list

    // Account security
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LastLoginDate { get; set; }
    public DateTime? AccountCreatedDate { get; set; } = DateTime.UtcNow;

    // Additional properties
    public string Username { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

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