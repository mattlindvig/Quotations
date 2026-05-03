using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Quotations.Api.Controllers;
using Quotations.Api.Models;
using Quotations.Api.Models.Dtos;
using Quotations.Api.Repositories;

namespace Quotations.Tests.Unit;

public class AuthControllerTests
{
    private readonly Mock<IUserRepository> _mockRepo;
    private readonly Mock<IPasswordHasher<User>> _mockHasher;
    private readonly Mock<IRefreshTokenRepository> _mockRefreshTokenRepo;
    private readonly IConfiguration _config;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _mockRepo = new Mock<IUserRepository>();
        _mockHasher = new Mock<IPasswordHasher<User>>();
        _mockRefreshTokenRepo = new Mock<IRefreshTokenRepository>();

        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:Secret", "test-secret-key-must-be-at-least-32-chars!" },
                { "Jwt:Issuer", "test-issuer" },
                { "Jwt:Audience", "test-audience" },
                { "Jwt:ExpirationMinutes", "60" }
            })
            .Build();

        _controller = new AuthController(_mockRepo.Object, _mockHasher.Object, _config, _mockRefreshTokenRepo.Object);
    }

    // -------------------------------------------------------------------------
    // Register
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Register_WithNewUser_Returns201AndToken()
    {
        _mockRepo.Setup(r => r.GetByUsernameAsync("newuser")).ReturnsAsync((User?)null);
        _mockRepo.Setup(r => r.GetByEmailAsync("new@example.com")).ReturnsAsync((User?)null);
        _mockHasher.Setup(h => h.HashPassword(It.IsAny<User>(), "Password1!")).Returns("hashed");
        _mockRepo.Setup(r => r.CreateAsync(It.IsAny<User>())).ReturnsAsync((User u) =>
        {
            u.Id = "507f1f77bcf86cd799439011";
            return u;
        });

        var result = await _controller.Register(new RegisterRequest
        {
            Username = "newuser",
            Email = "new@example.com",
            Password = "Password1!",
            DisplayName = "New User"
        });

        var created = result.Result as CreatedAtActionResult;
        created.Should().NotBeNull();
        var body = created!.Value as ApiResponse<AuthResponse>;
        body!.Success.Should().BeTrue();
        body.Data!.Token.Should().NotBeNullOrWhiteSpace();
        body.Data.User.Username.Should().Be("newuser");
    }

    [Fact]
    public async Task Register_WithDuplicateUsername_Returns409()
    {
        _mockRepo.Setup(r => r.GetByUsernameAsync("existing"))
            .ReturnsAsync(new User { Username = "existing" });

        var result = await _controller.Register(new RegisterRequest
        {
            Username = "existing",
            Email = "other@example.com",
            Password = "Password1!"
        });

        result.Result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task Register_WithShortPassword_Returns400()
    {
        var result = await _controller.Register(new RegisterRequest
        {
            Username = "newuser",
            Email = "new@example.com",
            Password = "short"
        });

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    // -------------------------------------------------------------------------
    // Login
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Login_WithValidCredentials_Returns200AndToken()
    {
        var user = new User
        {
            Id = "507f1f77bcf86cd799439011",
            Username = "testuser",
            DisplayName = "Test User",
            Roles = new List<string> { "User" },
            IsActive = true
        };

        _mockRepo.Setup(r => r.GetByUsernameAsync("testuser")).ReturnsAsync(user);
        _mockHasher.Setup(h => h.VerifyHashedPassword(user, user.PasswordHash, "Password1!"))
            .Returns(PasswordVerificationResult.Success);

        var result = await _controller.Login(new LoginRequest
        {
            Username = "testuser",
            Password = "Password1!"
        });

        var ok = result.Result as OkObjectResult;
        ok.Should().NotBeNull();
        var body = ok!.Value as ApiResponse<AuthResponse>;
        body!.Success.Should().BeTrue();
        body.Data!.Token.Should().NotBeNullOrWhiteSpace();
        body.Data.User.Id.Should().Be("507f1f77bcf86cd799439011");
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var user = new User { Username = "testuser", IsActive = true };
        _mockRepo.Setup(r => r.GetByUsernameAsync("testuser")).ReturnsAsync(user);
        _mockHasher.Setup(h => h.VerifyHashedPassword(user, user.PasswordHash, "wrong"))
            .Returns(PasswordVerificationResult.Failed);

        var result = await _controller.Login(new LoginRequest
        {
            Username = "testuser",
            Password = "wrong"
        });

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_WithUnknownUsername_Returns401()
    {
        _mockRepo.Setup(r => r.GetByUsernameAsync("nobody")).ReturnsAsync((User?)null);

        var result = await _controller.Login(new LoginRequest
        {
            Username = "nobody",
            Password = "Password1!"
        });

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_WithInactiveUser_Returns401()
    {
        var user = new User { Username = "banned", IsActive = false };
        _mockRepo.Setup(r => r.GetByUsernameAsync("banned")).ReturnsAsync(user);

        var result = await _controller.Login(new LoginRequest
        {
            Username = "banned",
            Password = "Password1!"
        });

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }
}
