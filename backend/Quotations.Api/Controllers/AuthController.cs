using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Quotations.Api.Configuration;
using Quotations.Api.Models;
using Quotations.Api.Models.Dtos;
using Quotations.Api.Repositories;
using Quotations.Api.Services;

namespace Quotations.Api.Controllers;

public record RefreshRequest(string RefreshToken);
public record LogoutRequest(string? RefreshToken);

/// <summary>
/// Handles user registration, login, token refresh, and logout.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private static readonly Regex UsernamePattern = new(@"^[a-zA-Z0-9_\-\.]{3,50}$", RegexOptions.Compiled);

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IEmailService _emailService;
    private readonly AppSettings _appSettings;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IUserRepository userRepository,
        IPasswordHasher<User> passwordHasher,
        IConfiguration configuration,
        IRefreshTokenRepository refreshTokenRepository,
        IEmailService emailService,
        IOptions<AppSettings> appSettings,
        ILogger<AuthController> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _refreshTokenRepository = refreshTokenRepository;
        _emailService = emailService;
        _appSettings = appSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Register a new user account.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), 201)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 409)]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) ||
            request.Username.Length < 3 || request.Username.Length > 50)
            return BadRequest(ApiResponse<object>.ErrorResponse("Username must be between 3 and 50 characters."));

        if (!UsernamePattern.IsMatch(request.Username))
            return BadRequest(ApiResponse<object>.ErrorResponse(
                "Username may only contain letters, numbers, underscores, hyphens, and periods."));

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return BadRequest(ApiResponse<object>.ErrorResponse("Password must be at least 8 characters."));

        if (!IsPasswordComplex(request.Password))
            return BadRequest(ApiResponse<object>.ErrorResponse(
                "Password must contain at least one uppercase letter, one lowercase letter, and one number or symbol."));

        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(ApiResponse<object>.ErrorResponse("A valid email address is required."));
        try { _ = new System.Net.Mail.MailAddress(request.Email); }
        catch (FormatException) { return BadRequest(ApiResponse<object>.ErrorResponse("A valid email address is required.")); }

        if (await _userRepository.GetByUsernameAsync(request.Username) is not null)
            return Conflict(ApiResponse<object>.ErrorResponse("Username is already taken."));

        if (await _userRepository.GetByEmailAsync(request.Email) is not null)
            return Conflict(ApiResponse<object>.ErrorResponse("Email address is already registered."));

        var user = new User
        {
            Username = request.Username.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? request.Username.Trim()
                : request.DisplayName.Trim(),
            Roles = new List<string> { "User" },
            EmailVerified = false
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        await _userRepository.CreateAsync(user);
        _logger.LogInformation("New user registered: {Username}", user.Username);

        var (rawVerifyToken, hashedVerifyToken) = GenerateSecureToken();
        var verifyExpiry = DateTime.UtcNow.AddHours(24);
        await _userRepository.SetEmailVerificationTokenAsync(user.Id, hashedVerifyToken, verifyExpiry);
        var verifyLink = $"{_appSettings.FrontendUrl}/verify-email?token={Uri.EscapeDataString(rawVerifyToken)}";
        await _emailService.SendEmailVerificationAsync(user.Email, user.DisplayName, verifyLink);

        var token = GenerateJwtToken(user);
        var refreshToken = await IssueRefreshTokenAsync(user.Id);
        return CreatedAtAction(
            nameof(Register),
            ApiResponse<AuthResponse>.SuccessResponse(BuildAuthResponse(token, refreshToken, user), "Registration successful."));
    }

    /// <summary>
    /// Authenticate with username and password, returning a JWT token.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return Unauthorized(ApiResponse<object>.ErrorResponse("Username and password are required."));

        var user = await _userRepository.GetByUsernameAsync(request.Username);
        if (user is null || !user.IsActive)
            return Unauthorized(ApiResponse<object>.ErrorResponse("Invalid username or password."));

        if (user.LockoutUntil.HasValue && user.LockoutUntil > DateTime.UtcNow)
        {
            _logger.LogWarning("Locked account login attempt: {Username}", request.Username);
            return Unauthorized(ApiResponse<object>.ErrorResponse(
                "Account is temporarily locked due to too many failed attempts. Please try again later."));
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            const int maxAttempts = 5;
            var lockoutUntil = user.FailedLoginCount + 1 >= maxAttempts
                ? DateTime.UtcNow.AddMinutes(15)
                : (DateTime?)null;
            await _userRepository.IncrementFailedLoginAsync(user.Id, lockoutUntil);
            _logger.LogWarning("Failed login attempt for user: {Username} (attempt {Count})",
                request.Username, user.FailedLoginCount + 1);
            return Unauthorized(ApiResponse<object>.ErrorResponse("Invalid username or password."));
        }

        await _userRepository.ResetFailedLoginAsync(user.Id);
        _logger.LogInformation("Successful login: {Username}", user.Username);
        var token = GenerateJwtToken(user);
        var refreshToken = await IssueRefreshTokenAsync(user.Id);
        return Ok(ApiResponse<AuthResponse>.SuccessResponse(BuildAuthResponse(token, refreshToken, user)));
    }

    /// <summary>
    /// Exchange a valid refresh token for a new access token and rotated refresh token.
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Refresh([FromBody] RefreshRequest request)
    {
        var existing = await _refreshTokenRepository.FindByTokenAsync(request.RefreshToken);
        if (existing is null)
            return Unauthorized(ApiResponse<object>.ErrorResponse("Invalid or expired refresh token."));

        var user = await _userRepository.GetByIdAsync(existing.UserId);
        if (user is null || !user.IsActive)
            return Unauthorized(ApiResponse<object>.ErrorResponse("User not found or inactive."));

        await _refreshTokenRepository.RevokeAsync(existing.Token);

        var newAccessToken = GenerateJwtToken(user);
        var newRefreshToken = await IssueRefreshTokenAsync(user.Id);
        return Ok(ApiResponse<AuthResponse>.SuccessResponse(BuildAuthResponse(newAccessToken, newRefreshToken, user)));
    }

    /// <summary>
    /// Revoke the current refresh token, ending the persistent session on this device.
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<ActionResult<ApiResponse<object>>> Logout([FromBody] LogoutRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
            await _refreshTokenRepository.RevokeAsync(request.RefreshToken);

        return Ok(ApiResponse<object>.SuccessResponse(null, "Logged out successfully."));
    }

    private async Task<string> IssueRefreshTokenAsync(string userId)
    {
        var tokenBytes = new byte[64];
        RandomNumberGenerator.Fill(tokenBytes);
        var tokenValue = Convert.ToBase64String(tokenBytes);

        var refreshToken = new RefreshToken
        {
            Token = tokenValue,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
        };

        await _refreshTokenRepository.CreateAsync(refreshToken);
        return tokenValue;
    }

    private string GenerateJwtToken(User user)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var secret = jwtSettings["Secret"]
            ?? throw new InvalidOperationException("JWT Secret is not configured.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new("unique_name", user.Username),
            new("displayName", user.DisplayName),
        };

        foreach (var role in user.Roles)
            claims.Add(new Claim("role", role));

        var expirationMinutes = int.TryParse(jwtSettings["ExpirationMinutes"], out var mins) ? mins : 60;

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Send a password reset link to the provided email address.
    /// Always returns 200 to avoid leaking whether an account exists.
    /// </summary>
    [HttpPost("forgot-password")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return Ok();

        var user = await _userRepository.GetByEmailAsync(request.Email.Trim().ToLowerInvariant());
        if (user is null || !user.IsActive)
            return Ok();

        var (rawToken, hashedToken) = GenerateSecureToken();
        var expiry = DateTime.UtcNow.AddHours(1);
        await _userRepository.SetPasswordResetTokenAsync(user.Id, hashedToken, expiry);

        var resetLink = $"{_appSettings.FrontendUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";
        await _emailService.SendPasswordResetAsync(user.Email, user.DisplayName, resetLink);
        _logger.LogInformation("Password reset requested for: {Username}", user.Username);

        return Ok();
    }

    /// <summary>
    /// Reset password using a valid reset token.
    /// </summary>
    [HttpPost("reset-password")]
    [ProducesResponseType(200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(ApiResponse<object>.ErrorResponse("Token and new password are required."));

        if (request.NewPassword.Length < 8 || !IsPasswordComplex(request.NewPassword))
            return BadRequest(ApiResponse<object>.ErrorResponse(
                "Password must be at least 8 characters and contain uppercase, lowercase, and a number or symbol."));

        var (_, hashedToken) = (string.Empty, HashToken(request.Token));
        var placeholder = new User();
        var newHash = _passwordHasher.HashPassword(placeholder, request.NewPassword);

        var success = await _userRepository.ResetPasswordAsync(hashedToken, newHash);
        if (!success)
            return BadRequest(ApiResponse<object>.ErrorResponse("This reset link is invalid or has expired."));

        _logger.LogInformation("Password successfully reset via token");
        return Ok(ApiResponse<object>.SuccessResponse(null, "Password reset successfully. You can now log in."));
    }

    /// <summary>
    /// Verify email address using a token from the verification email.
    /// </summary>
    [HttpPost("verify-email")]
    [ProducesResponseType(200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(ApiResponse<object>.ErrorResponse("Verification token is required."));

        var hashedToken = HashToken(request.Token);
        var success = await _userRepository.VerifyEmailAsync(hashedToken);
        if (!success)
            return BadRequest(ApiResponse<object>.ErrorResponse("This verification link is invalid or has expired."));

        _logger.LogInformation("Email verified via token");
        return Ok(ApiResponse<object>.SuccessResponse(null, "Email verified successfully."));
    }

    /// <summary>
    /// Resend the email verification link.
    /// Always returns 200 to avoid leaking whether an account exists.
    /// </summary>
    [HttpPost("resend-verification")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> ResendVerification([FromBody] ForgotPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return Ok();

        var user = await _userRepository.GetByEmailAsync(request.Email.Trim().ToLowerInvariant());
        if (user is null || !user.IsActive || user.EmailVerified)
            return Ok();

        var (rawToken, hashedToken) = GenerateSecureToken();
        var expiry = DateTime.UtcNow.AddHours(24);
        await _userRepository.SetEmailVerificationTokenAsync(user.Id, hashedToken, expiry);

        var verifyLink = $"{_appSettings.FrontendUrl}/verify-email?token={Uri.EscapeDataString(rawToken)}";
        await _emailService.SendEmailVerificationAsync(user.Email, user.DisplayName, verifyLink);

        return Ok();
    }

    private static AuthResponse BuildAuthResponse(string token, string refreshToken, User user) => new()
    {
        Token = token,
        RefreshToken = refreshToken,
        User = new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Roles = user.Roles,
            EmailVerified = user.EmailVerified
        }
    };

    private static bool IsPasswordComplex(string password) =>
        password.Any(char.IsUpper) &&
        password.Any(char.IsLower) &&
        password.Any(c => char.IsDigit(c) || !char.IsLetterOrDigit(c));

    private static (string raw, string hashed) GenerateSecureToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        var raw = Convert.ToBase64String(bytes);
        return (raw, HashToken(raw));
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
