using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using LogiTrack.Models;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;

    public AuthController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IConfiguration config, ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _config = config;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        // Input validation
        if (req == null || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
        {
            _logger.LogWarning("Registration attempt with invalid input");
            return BadRequest("Email and password are required.");
        }

        // Validate email format
        if (!new EmailAddressAttribute().IsValid(req.Email))
        {
            _logger.LogWarning("Registration attempt with invalid email format: {Email}", req.Email);
            return BadRequest("Invalid email format.");
        }

        // Enforce max password length
        if (req.Password.Length > 128)
        {
            return BadRequest("Password exceeds maximum length.");
        }

        var user = new ApplicationUser { UserName = req.Email, Email = req.Email };
        var result = await _userManager.CreateAsync(user, req.Password);

        if (!result.Succeeded)
        {
            // Log failure internally, return generic message (prevent account enumeration)
            _logger.LogWarning("Registration failed for email {Email}: {Errors}", req.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
            return BadRequest("Registration failed. Please ensure your password meets complexity requirements.");
        }

        // Assign default "User" role
        await _userManager.AddToRoleAsync(user, "User");

        // Log successful registration
        _logger.LogInformation("User registered successfully: {UserId}", user.Id);

        return Ok(new { message = "Registration successful. Please check your email to confirm your account.", userId = user.Id });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        // Input validation
        if (req == null || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
        {
            _logger.LogWarning("Login attempt with invalid input");
            return Unauthorized("Invalid credentials.");
        }

        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user == null)
        {
            // Log failed attempt, return generic message (prevent account enumeration)
            _logger.LogWarning("Login failed: user not found for email {Email}", req.Email);
            return Unauthorized("Invalid credentials.");
        }

        // Check if email is confirmed (enforce email verification)
        if (!user.EmailConfirmed)
        {
            _logger.LogWarning("Login attempt for unconfirmed email: {Email}", req.Email);
            return Unauthorized("Please confirm your email before logging in.");
        }

        // Check if password has expired
        if (user.IsPasswordExpired())
        {
            _logger.LogWarning("Login attempt with expired password: {Email}", req.Email);
            return StatusCode(403, "Your password has expired. Please reset your password.");
        }

        // Check password and account lockout
        var signInResult = await _signInManager.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
        
        if (signInResult.IsLockedOut)
        {
            _logger.LogWarning("Account locked due to multiple failed login attempts: {Email}", req.Email);
            return StatusCode(429, "Account locked due to multiple failed login attempts. Try again in 15 minutes.");
        }

        if (!signInResult.Succeeded)
        {
            _logger.LogWarning("Failed login attempt for user {Email}", req.Email);
            return Unauthorized("Invalid credentials.");
        }

        // Reset lockout counter on successful login
        await _userManager.ResetAccessFailedCountAsync(user);

        // Update last login date
        user.LastLoginDate = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var token = await GenerateJwtToken(user);
        _logger.LogInformation("User logged in successfully: {UserId}", user.Id);
        return Ok(new { token });
    }

    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.UserId) || string.IsNullOrWhiteSpace(req.Token))
        {
            return BadRequest("UserId and token are required.");
        }

        var user = await _userManager.FindByIdAsync(req.UserId);
        if (user == null)
        {
            _logger.LogWarning("Email confirmation attempt with invalid user ID: {UserId}", req.UserId);
            return BadRequest("User not found.");
        }

        var result = await _userManager.ConfirmEmailAsync(user, req.Token);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Email confirmation failed for user {UserId}", user.Id);
            return BadRequest("Email confirmation failed. Token may be invalid or expired.");
        }

        _logger.LogInformation("Email confirmed for user {UserId}", user.Id);
        return Ok("Email confirmed successfully. You can now log in.");
    }

    [HttpPost("request-email-confirmation")]
    public async Task<IActionResult> RequestEmailConfirmation([FromBody] RequestEmailConfirmationRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Email))
        {
            return BadRequest("Email is required.");
        }

        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user == null)
        {
            // Return generic message for security
            return Ok("If an account exists with this email, a confirmation link will be sent.");
        }

        if (user.EmailConfirmed)
        {
            return Ok("Email is already confirmed.");
        }

        // Generate confirmation token
        var confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);

        // TODO: Send email with confirmation link
        // Example: await _emailService.SendConfirmationEmailAsync(user.Email, user.Id, confirmationToken);
        _logger.LogInformation("Email confirmation token generated for user {UserId}", user.Id);

        return Ok("If an account exists with this email, a confirmation link will be sent.");
    }

    private async Task<string> GenerateJwtToken(ApplicationUser user)
    {
        var jwt = _config.GetSection("Jwt");
        var key = jwt["Key"] ?? throw new InvalidOperationException("JWT Key not configured");
        var issuer = jwt["Issuer"] ?? "LogiTrack";
        var audience = jwt["Audience"] ?? "LogiTrackUsers";
        var expiresMinutes = int.TryParse(jwt["ExpiresMinutes"], out var m) ? m : 60;

        // Fetch user roles and add to claims
        var roles = await _userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(ClaimTypes.Name, user.UserName ?? string.Empty)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var keyBytes = Encoding.UTF8.GetBytes(key);
        var securityKey = new SymmetricSecurityKey(keyBytes);
        var creds = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public record RegisterRequest(string Email, string Password);
    public record LoginRequest(string Email, string Password);
    public record ConfirmEmailRequest(string UserId, string Token);
    public record RequestEmailConfirmationRequest(string Email);
}
