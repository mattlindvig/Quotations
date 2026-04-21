using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Quotations.Api.Models;
using Quotations.Api.Models.Dtos;
using Quotations.Api.Repositories;

namespace Quotations.Api.Controllers;

/// <summary>
/// Handles user registration and login, issuing JWT tokens on success.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IConfiguration _configuration;

    public AuthController(
        IUserRepository userRepository,
        IPasswordHasher<User> passwordHasher,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
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

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return BadRequest(ApiResponse<object>.ErrorResponse("Password must be at least 8 characters."));

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            return BadRequest(ApiResponse<object>.ErrorResponse("A valid email address is required."));

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
            Roles = new List<string> { "User" }
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        await _userRepository.CreateAsync(user);

        var token = GenerateJwtToken(user);
        return CreatedAtAction(
            nameof(Register),
            ApiResponse<AuthResponse>.SuccessResponse(BuildAuthResponse(token, user), "Registration successful."));
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

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
            return Unauthorized(ApiResponse<object>.ErrorResponse("Invalid username or password."));

        var token = GenerateJwtToken(user);
        return Ok(ApiResponse<AuthResponse>.SuccessResponse(BuildAuthResponse(token, user)));
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
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Username),
            new("displayName", user.DisplayName),
        };

        foreach (var role in user.Roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var expirationMinutes = int.TryParse(jwtSettings["ExpirationMinutes"], out var mins) ? mins : 60;

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static AuthResponse BuildAuthResponse(string token, User user) => new()
    {
        Token = token,
        User = new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Roles = user.Roles
        }
    };
}
