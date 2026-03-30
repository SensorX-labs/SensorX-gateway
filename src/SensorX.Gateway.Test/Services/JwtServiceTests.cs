using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Moq;
using SensorX.Gateway.Application.Interfaces;
using SensorX.Gateway.Infrastructure.Services;
using System.Security.Claims;
using System.Security.Cryptography;
using Xunit;

namespace SensorX.Gateway.Test.Services;

public class JwtServiceTests
{
    private readonly Mock<IKeyManagementService> _mockKeyManager;
    private readonly IConfiguration _configuration;
    private readonly RsaSecurityKey _testSigningKey;
    private readonly string _kid = "test-key-id";
    private readonly JwtService _service;

    public JwtServiceTests()
    {
        var rsa = RSA.Create(2048);
        _testSigningKey = new RsaSecurityKey(rsa) { KeyId = _kid };

        _mockKeyManager = new Mock<IKeyManagementService>();
        _mockKeyManager.Setup(x => x.GetSigningCredentials())
            .Returns(new SigningCredentials(_testSigningKey, SecurityAlgorithms.RsaSha256));
        _mockKeyManager.Setup(x => x.GetKid()).Returns(_kid);
        _mockKeyManager.Setup(x => x.ResolveSigningKey(It.IsAny<string>(), It.IsAny<SecurityToken>(), It.IsAny<string>(), It.IsAny<TokenValidationParameters>()))
            .Returns(new[] { _testSigningKey });

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Issuer"] = "https://test-issuer.com",
                ["JwtSettings:Audience"] = "test-audience",
                ["JwtSettings:AccessTokenMinutes"] = "15"
            })
            .Build();

        _service = new JwtService(_mockKeyManager.Object, _configuration);
    }

    [Fact]
    public void Sign_ShouldProduceValidJwt()
    {
        // Arrange
        var claims = new List<Claim> { new("sub", "user-123"), new("role", "admin") };

        // Act
        var token = _service.Sign(claims);

        // Assert
        token.Should().NotBeNullOrEmpty();
        token.Split('.').Should().HaveCount(3); // Header, Payload, Signature
    }

    [Fact]
    public void ValidateToken_ShouldReturnPrincipalWithCorrectClaims()
    {
        // Arrange
        var claims = new List<Claim> { new("sub", "user-123"), new("role", "admin") };
        var token = _service.Sign(claims);

        // Act
        var principal = _service.ValidateToken(token);

        // Assert
        principal.Should().NotBeNull();
        principal.FindFirst("sub")?.Value.Should().Be("user-123");
        principal.FindFirst("role")?.Value.Should().Be("admin");
        principal.FindFirst("iss")?.Value.Should().Be("https://test-issuer.com");
        principal.FindFirst("aud")?.Value.Should().Be("test-audience");
        principal.FindFirst("jti").Should().NotBeNull();
        principal.FindFirst("iat").Should().NotBeNull();
    }

    [Fact]
    public void ValidateToken_WithInvalidToken_ShouldReturnNull()
    {
        // Act
        var principal = _service.ValidateToken("invalid.token.string");

        // Assert
        principal.Should().BeNull();
    }
}
