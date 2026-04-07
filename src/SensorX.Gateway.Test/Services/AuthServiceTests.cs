using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SensorX.Gateway.Application.DTOs;
using SensorX.Gateway.Application.Services;
using SensorX.Gateway.Domain.Entities;
using SensorX.Gateway.Domain.Interfaces;
using SensorX.Gateway.Domain.Interfaces.Repositories;
using Xunit;

namespace SensorX.Gateway.Test.Services;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IRoleRepository> _mockRoleRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IJwtService> _mockJwtService;
    private readonly Mock<IAccessTokenService> _mockAccessTokenService;
    private readonly Mock<IRefreshTokenService> _mockRefreshTokenService;
    private readonly Mock<IRedisPermissionService> _mockPermissionService;
    private readonly Mock<IPasswordHasher> _mockPasswordHasher;
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<AuthService>> _mockLogger;
    
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockRoleRepository = new Mock<IRoleRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockJwtService = new Mock<IJwtService>();
        _mockAccessTokenService = new Mock<IAccessTokenService>();
        _mockRefreshTokenService = new Mock<IRefreshTokenService>();
        _mockPermissionService = new Mock<IRedisPermissionService>();
        _mockPasswordHasher = new Mock<IPasswordHasher>();
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:MaxLoginAttempts"] = "5"
            })
            .Build();
        _mockLogger = new Mock<ILogger<AuthService>>();

        _authService = new AuthService(
            _mockUserRepository.Object,
            _mockRoleRepository.Object,
            _mockUnitOfWork.Object,
            _mockJwtService.Object,
            _mockAccessTokenService.Object,
            _mockRefreshTokenService.Object,
            _mockPermissionService.Object,
            _mockPasswordHasher.Object,
            _configuration,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsSuccess()
    {
        var email = "test@example.com";
        var request = new LoginRequest(email, "pass123");
        var user = User.Create(email, "hash");

        _mockUserRepository.Setup(x => x.GetByEmailAsync(email)).ReturnsAsync(user);
        _mockPasswordHasher.Setup(x => x.VerifyAsync("pass123", "hash")).ReturnsAsync(true);
        _mockAccessTokenService.Setup(x => x.CreateToken(user.Id, email, "", "")).Returns("access_token");
        _mockRefreshTokenService.Setup(x => x.CreateAsync(user.Id, It.IsAny<int>())).ReturnsAsync("refresh_token");

        var result = await _authService.LoginAsync(request);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsFail()
    {
        var email = "test@example.com";
        var request = new LoginRequest(email, "wrong");
        var user = User.Create(email, "hash");

        _mockUserRepository.Setup(x => x.GetByEmailAsync(email)).ReturnsAsync(user);
        _mockPasswordHasher.Setup(x => x.VerifyAsync("wrong", "hash")).ReturnsAsync(false);

        var result = await _authService.LoginAsync(request);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid credentials");
    }
}
