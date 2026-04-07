using FluentAssertions;
using Microsoft.Extensions.Configuration;
using SensorX.Gateway.Infrastructure.Services;
using System.Security.Claims;
using Xunit;

namespace SensorX.Gateway.Test.Services;

public class JwtServiceTests
{
    private readonly IConfiguration _configuration;
    private readonly JwtService _service;

    public JwtServiceTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Issuer"] = "https://test-issuer.com",
                ["JwtSettings:Audience"] = "test-audience",
                ["JwtSettings:AccessTokenMinutes"] = "15",
                ["JwtSettings:HmacSecret"] = "test-secret-key-must-be-long-enough-32-chars-long"
            })
            .Build();

        _service = new JwtService(_configuration);
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
        principal!.FindFirst("sub")?.Value.Should().Be("user-123");
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
