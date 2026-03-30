using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SensorX.Gateway.Api.Controllers;
using SensorX.Gateway.Application.DTOs;
using SensorX.Gateway.Application.Interfaces;
using SensorX.Gateway.Domain.Entities;
using SensorX.Gateway.Domain.Interfaces;
using SensorX.Gateway.Infrastructure.Persistence;
using Xunit;

namespace SensorX.Gateway.Test.Controllers;

public class AuthControllerTests
{
    private readonly AppDbContext _dbContext;
    private readonly Mock<IJwtService> _mockJwtService;
    private readonly Mock<IAccessTokenService> _mockAccessTokenService;
    private readonly Mock<IRefreshTokenService> _mockRefreshTokenService;
    private readonly Mock<IRedisPermissionService> _mockPermissionService;
    private readonly Mock<IIdempotencyService> _mockIdempotencyService;
    private readonly Mock<IPasswordHasher> _mockPasswordHasher;
    private readonly Mock<ITokenBlacklistService> _mockBlacklistService;
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<AuthController>> _mockLogger;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);

        _mockJwtService = new Mock<IJwtService>();
        _mockAccessTokenService = new Mock<IAccessTokenService>();
        _mockRefreshTokenService = new Mock<IRefreshTokenService>();
        _mockPermissionService = new Mock<IRedisPermissionService>();
        _mockIdempotencyService = new Mock<IIdempotencyService>();
        _mockPasswordHasher = new Mock<IPasswordHasher>();
        _mockBlacklistService = new Mock<ITokenBlacklistService>();
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:MaxLoginAttempts"] = "5"
            })
            .Build();
        _mockLogger = new Mock<ILogger<AuthController>>();

        // Default setups
        _mockPermissionService
            .Setup(x => x.SetPermissionsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<string>>()))
            .Returns(Task.CompletedTask);
        _mockPermissionService
            .Setup(x => x.RemovePermissionsAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _controller = new AuthController(
            _dbContext,
            _mockJwtService.Object,
            _mockAccessTokenService.Object,
            _mockRefreshTokenService.Object,
            _mockPermissionService.Object,
            _mockIdempotencyService.Object,
            _mockPasswordHasher.Object,
            _mockBlacklistService.Object,
            _configuration,
            _mockLogger.Object);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    #region Login Tests

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithTokenPair()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "user@example.com";
        var password = "password123";
        var request = new LoginRequest(email, password);

        var roleId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = email,
            PasswordHash = "hashedpassword",
            MfaEnabled = false,
            IsLocked = false,
            UserRoles = new List<UserRole>
            {
                new UserRole
                {
                    UserId = userId,
                    RoleId = roleId,
                    Role = new Role { Id = roleId, Name = "user" }
                }
            }
        };

        var accessToken = "access_token_jwt";
        var refreshToken = "refresh_token";

        _seedUsers(user);
        _mockPasswordHasher.Setup(x => x.VerifyAsync(password, user.PasswordHash))
            .ReturnsAsync(true);
        _mockAccessTokenService.Setup(x => x.CreateToken(userId, email, "user", "user"))
            .Returns(accessToken);
        _mockRefreshTokenService.Setup(x => x.CreateAsync(userId, It.IsAny<int>()))
            .ReturnsAsync(refreshToken);

        // Act
        var result = await _controller.Login(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult?.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task Login_WithInvalidEmail_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest("nonexistent@example.com", "password123");
        // No users seeded — InMemory DB is empty by default

        // Act
        var result = await _controller.Login(request);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "user@example.com";
        var request = new LoginRequest(email, "wrongpassword");

        var user = new User
        {
            Id = userId,
            Email = email,
            PasswordHash = "hashedpassword",
            IsLocked = false,
            LoginFailCount = 0
        };

        _seedUsers(user);
        _mockPasswordHasher.Setup(x => x.VerifyAsync(request.Password, user.PasswordHash))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.Login(request);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_WithLockedAccount_Returns423()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new LoginRequest("locked@example.com", "password");

        var user = new User
        {
            Id = userId,
            Email = request.Email,
            PasswordHash = "hashedpassword",
            IsLocked = true,
            LockedUntil = DateTimeOffset.UtcNow.AddMinutes(10)
        };

        _seedUsers(user);

        // Act
        var result = await _controller.Login(request);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult?.StatusCode.Should().Be(423);
    }

    [Fact]
    public async Task Login_WithMfaEnabled_ReturnsMfaChallenge()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "mfa@example.com";
        var password = "password123";
        var request = new LoginRequest(email, password);
        var mfaToken = "mfa_token_jwt";

        var user = new User
        {
            Id = userId,
            Email = email,
            PasswordHash = "hashedpassword",
            MfaEnabled = true,
            IsLocked = false
        };

        _seedUsers(user);
        _mockPasswordHasher.Setup(x => x.VerifyAsync(password, user.PasswordHash))
            .ReturnsAsync(true);
        _mockAccessTokenService.Setup(x => x.CreateMfaToken(userId))
            .Returns(mfaToken);

        // Act
        var result = await _controller.Login(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Login_WhenSuccessful_CallsAccessTokenServiceWithCorrectParams()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "test@example.com";
        var password = "password123";
        var roleId = Guid.NewGuid();
        var user = new User
        {
            Id = userId, Email = email, PasswordHash = "hash",
            UserRoles = new List<UserRole> { 
                new UserRole { Role = new Role { Id = roleId, Name = "admin" } } 
            }
        };
        _seedUsers(user);
        _mockPasswordHasher.Setup(x => x.VerifyAsync(password, It.IsAny<string>())).ReturnsAsync(true);
        
        // Act
        await _controller.Login(new LoginRequest(email, password));

        // Assert
        _mockAccessTokenService.Verify(x => x.CreateToken(userId, email, "admin", "admin"), Times.Once);
    }

    #endregion

    #region Register Tests

    [Fact]
    public async Task Register_WithValidData_ReturnsCreatedAtRoute()
    {
        // Arrange
        var request = new RegisterRequest("newuser@example.com", "SecurePassword123!");
        var passwordHash = "hashedpassword";

        var defaultRole = new Role { Id = Guid.NewGuid(), Name = "user" };

        _seedRoles(defaultRole);
        _mockPasswordHasher.Setup(x => x.HashAsync(request.Password))
            .ReturnsAsync(passwordHash);

        // Act
        var result = await _controller.Register(request, null);

        // Assert
        result.Should().BeOfType<CreatedResult>();
        var createdResult = result as CreatedResult;
        createdResult?.StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsConflict()
    {
        // Arrange
        var email = "existing@example.com";
        var request = new RegisterRequest(email, "password");

        var existingUser = new User { Id = Guid.NewGuid(), Email = email, PasswordHash = "hash" };
        _seedUsers(existingUser);

        // Act
        var result = await _controller.Register(request, null);

        // Assert
        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task Register_WithIdempotencyKey_CachesResponse()
    {
        // Arrange
        var idempotencyKey = "test-idempotency-key";
        var cachedResponse = new { message = "User registered", userId = Guid.NewGuid() };
        var request = new RegisterRequest("user@example.com", "password");

        _mockIdempotencyService.Setup(x => x.GetCachedResponseAsync(idempotencyKey))
            .ReturnsAsync(cachedResponse);

        // Act
        var result = await _controller.Register(request, idempotencyKey);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockIdempotencyService.Verify(x => x.GetCachedResponseAsync(idempotencyKey), Times.Once);
    }

    #endregion

    #region Logout Tests

    [Fact]
    public async Task Logout_WithValidToken_ReturnsOk()
    {
        // Arrange
        var request = new LogoutRequest("refresh_token");

        _mockRefreshTokenService.Setup(x => x.RevokeAsync(request.RefreshToken))
            .Returns(Task.CompletedTask);
        _mockPermissionService.Setup(x => x.RemovePermissionsAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
        _mockBlacklistService.Setup(x => x.BlacklistAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Logout(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region Refresh Tests

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewTokenPair()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new RefreshRequest("valid_refresh_token");
        var newAccessToken = "new_access_token";
        var newRefreshToken = "new_refresh_token";

        var roleId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "user@example.com",
            PasswordHash = "hash",
            IsLocked = false,
            UserRoles = new List<UserRole>
            {
                new UserRole
                {
                    UserId = userId,
                    RoleId = roleId,
                    Role = new Role { Id = roleId, Name = "user" }
                }
            }
        };

        _mockRefreshTokenService.Setup(x => x.RefreshAsync(request.RefreshToken))
            .ReturnsAsync((userId, newRefreshToken));
        _seedUsers(user);
        _mockAccessTokenService.Setup(x => x.CreateToken(userId, user.Email, "user", "user"))
            .Returns(newAccessToken);

        // Act
        var result = await _controller.Refresh(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Refresh_WithInvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        var request = new RefreshRequest("invalid_refresh_token");

        _mockRefreshTokenService.Setup(x => x.RefreshAsync(request.RefreshToken))
            .ThrowsAsync(new InvalidOperationException("Invalid refresh token"));

        // Act
        var result = await _controller.Refresh(request);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    #endregion

    #region Introspect Tests

    [Fact]
    public void Introspect_WithValidToken_ReturnsActiveToken()
    {
        // Arrange
        var token = "valid_jwt_token";
        var request = new IntrospectRequest(token);

        _mockJwtService.Setup(x => x.ValidateToken(token))
            .Returns(_createClaimsPrincipal("user-id", "api"));

        // Act
        var result = _controller.Introspect(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void Introspect_WithInvalidToken_ReturnsInactive()
    {
        // Arrange
        var request = new IntrospectRequest("invalid_token");
        _mockJwtService.Setup(x => x.ValidateToken("invalid_token"))
            .Returns((System.Security.Claims.ClaimsPrincipal?)null);

        // Act
        var result = _controller.Introspect(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region Helper Methods

    private void _seedUsers(params User[] users)
    {
        _dbContext.Users.AddRange(users);
        _dbContext.SaveChanges();
    }

    private void _seedRoles(params Role[] roles)
    {
        _dbContext.Roles.AddRange(roles);
        _dbContext.SaveChanges();
    }

    private System.Security.Claims.ClaimsPrincipal _createClaimsPrincipal(string userId, string scope)
    {
        var claims = new List<System.Security.Claims.Claim>
        {
            new System.Security.Claims.Claim("sub", userId),
            new System.Security.Claims.Claim("scope", scope),
            new System.Security.Claims.Claim("exp", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString())
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims);
        return new System.Security.Claims.ClaimsPrincipal(identity);
    }

    #endregion
}
