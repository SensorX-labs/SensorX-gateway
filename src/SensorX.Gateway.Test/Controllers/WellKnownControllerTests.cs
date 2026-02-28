using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using SensorX.Gateway.Api.Controllers;
using SensorX.Gateway.Application.Interfaces;
using System.Text.Json;
using Xunit;

namespace SensorX.Gateway.Test.Controllers;

public class WellKnownControllerTests
{
    private readonly Mock<IKeyManagementService> _mockKeyManager;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly WellKnownController _controller;

    public WellKnownControllerTests()
    {
        _mockKeyManager = new Mock<IKeyManagementService>();
        _mockConfiguration = new Mock<IConfiguration>();
        _controller = new WellKnownController(_mockKeyManager.Object, _mockConfiguration.Object);
    }

    #region JWKS Tests

    [Fact]
    public void GetJwks_ReturnsOkWithJwksResponse()
    {
        // Arrange
        var jwksResponse = new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    kid = "key-id-1",
                    n = "test-modulus",
                    e = "AQAB"
                }
            }
        };

        _mockKeyManager.Setup(x => x.GetJwksResponse())
            .Returns(jwksResponse);

        // Act
        var result = _controller.GetJwks();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult?.Value.Should().BeEquivalentTo(jwksResponse);
    }

    [Fact]
    public void GetJwks_VerifiesKeyManagerCalled()
    {
        // Arrange
        _mockKeyManager.Setup(x => x.GetJwksResponse())
            .Returns(new { keys = Array.Empty<object>() });

        // Act
        _controller.GetJwks();

        // Assert
        _mockKeyManager.Verify(x => x.GetJwksResponse(), Times.Once);
    }

    [Fact]
    public void GetJwks_ReturnsValidJwksStructure()
    {
        // Arrange
        var jwksResponse = new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    kid = "test-kid",
                    n = "modulus",
                    e = "AQAB"
                }
            }
        };

        _mockKeyManager.Setup(x => x.GetJwksResponse())
            .Returns(jwksResponse);

        // Act
        var result = _controller.GetJwks();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult?.Value.Should().NotBeNull();
    }

    #endregion

    #region OpenID Configuration Tests

    [Fact]
    public void GetOpenIdConfiguration_ReturnsOkWithConfiguration()
    {
        // Arrange
        var issuer = "https://gateway.example.com";
        _mockConfiguration.Setup(x => x["JwtSettings:Issuer"])
            .Returns(issuer);

        // Act
        var result = _controller.GetOpenIdConfiguration();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void GetOpenIdConfiguration_IncludesRequiredEndpoints()
    {
        // Arrange
        var issuer = "https://gateway.example.com";
        _mockConfiguration.Setup(x => x["JwtSettings:Issuer"])
            .Returns(issuer);

        // Act
        var result = _controller.GetOpenIdConfiguration();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult?.Value.Should().NotBeNull();

        var json = JsonSerializer.Serialize(okResult!.Value);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("issuer").GetString().Should().Be(issuer);
        doc.RootElement.GetProperty("jwks_uri").GetString().Should().Contain("/.well-known/jwks.json");
        doc.RootElement.GetProperty("authorization_endpoint").GetString().Should().Contain("/auth/login");
        doc.RootElement.GetProperty("token_endpoint").GetString().Should().Contain("/auth/token");
        doc.RootElement.GetProperty("introspection_endpoint").GetString().Should().Contain("/auth/introspect");
        doc.RootElement.GetProperty("revocation_endpoint").GetString().Should().Contain("/auth/revoke");
    }

    [Fact]
    public void GetOpenIdConfiguration_IncludesSupportedGrantTypes()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["JwtSettings:Issuer"])
            .Returns("https://gateway.example.com");

        // Act
        var result = _controller.GetOpenIdConfiguration();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;

        var json = JsonSerializer.Serialize(okResult!.Value);
        var doc = JsonDocument.Parse(json);
        var grantTypes = doc.RootElement.GetProperty("grant_types_supported")
            .EnumerateArray().Select(e => e.GetString()!).ToArray();
        grantTypes.Should().Contain(new[] { "password", "refresh_token", "client_credentials" });
    }

    [Fact]
    public void GetOpenIdConfiguration_IncludesSupportedResponseTypes()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["JwtSettings:Issuer"])
            .Returns("https://gateway.example.com");

        // Act
        var result = _controller.GetOpenIdConfiguration();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;

        var json = JsonSerializer.Serialize(okResult!.Value);
        var doc = JsonDocument.Parse(json);
        var responseTypes = doc.RootElement.GetProperty("response_types_supported")
            .EnumerateArray().Select(e => e.GetString()!).ToArray();
        responseTypes.Should().Contain("token");
    }

    [Fact]
    public void GetOpenIdConfiguration_WithDefaultIssuer_UsesDefaultValue()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["JwtSettings:Issuer"])
            .Returns((string?)null);

        // Act
        var result = _controller.GetOpenIdConfiguration();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;

        var json = JsonSerializer.Serialize(okResult!.Value);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("issuer").GetString().Should().Be("https://gateway.yourdomain.com");
    }

    [Fact]
    public void GetOpenIdConfiguration_SigningAlgorithmIsRS256()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["JwtSettings:Issuer"])
            .Returns("https://gateway.example.com");

        // Act
        var result = _controller.GetOpenIdConfiguration();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;

        var json = JsonSerializer.Serialize(okResult!.Value);
        var doc = JsonDocument.Parse(json);
        var signingAlgs = doc.RootElement.GetProperty("id_token_signing_alg_values_supported")
            .EnumerateArray().Select(e => e.GetString()!).ToArray();
        signingAlgs.Should().Contain("RS256");
    }

    [Fact]
    public void GetOpenIdConfiguration_IncludesAuthMethods()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["JwtSettings:Issuer"])
            .Returns("https://gateway.example.com");

        // Act
        var result = _controller.GetOpenIdConfiguration();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;

        var json = JsonSerializer.Serialize(okResult!.Value);
        var doc = JsonDocument.Parse(json);
        var authMethods = doc.RootElement.GetProperty("token_endpoint_auth_methods_supported")
            .EnumerateArray().Select(e => e.GetString()!).ToArray();
        authMethods.Should().Contain("client_secret_post");
    }

    #endregion
}
