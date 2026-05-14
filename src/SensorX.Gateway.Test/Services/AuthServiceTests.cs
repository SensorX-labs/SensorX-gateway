using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SensorX.Gateway.Application.DTOs;
using SensorX.Gateway.Application.Services;
using SensorX.Gateway.Domain.Entities;
using SensorX.Gateway.Domain.Enums;
using SensorX.Gateway.Domain.Interfaces;
using SensorX.Gateway.Domain.Interfaces.Repositories;
using Xunit;

namespace SensorX.Gateway.Test.Services;

public class AuthServiceTests
{
    private readonly Mock<IAccountRepository> _mockAccountRepository;
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
        _mockAccountRepository = new Mock<IAccountRepository>();
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
            _mockAccountRepository.Object,
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
        var account = Account.Create(email, "Test Name", "hash", Role.SaleStaff);

        _mockAccountRepository.Setup(x => x.GetByEmailAsync(email)).ReturnsAsync(account);
        _mockPasswordHasher.Setup(x => x.VerifyAsync("pass123", "hash")).ReturnsAsync(true);
        _mockAccessTokenService.Setup(x => x.CreateToken(account.Id, email, "SaleStaff", "SaleStaff", It.IsAny<Guid?>())).Returns("access_token");
        _mockRefreshTokenService.Setup(x => x.CreateAsync(account.Id, 30)).ReturnsAsync("refresh_token");

        var result = await _authService.LoginAsync(request);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsFail()
    {
        var email = "test@example.com";
        var request = new LoginRequest(email, "wrong");
        var account = Account.Create(email, "Test Name", "hash", Role.SaleStaff);

        _mockAccountRepository.Setup(x => x.GetByEmailAsync(email)).ReturnsAsync(account);
        _mockPasswordHasher.Setup(x => x.VerifyAsync("wrong", "hash")).ReturnsAsync(false);

        var result = await _authService.LoginAsync(request);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid credentials");
    }
}
