using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SensorX.Gateway.Api.Controllers;
using SensorX.Gateway.Application.DTOs;
using SensorX.Gateway.Application.Interfaces;
using Xunit;

namespace SensorX.Gateway.Test.Controllers;

public class TokenControllerTests
{
    private readonly Mock<IServiceTokenService> _mockServiceTokenService;
    private readonly TokenController _controller;

    public TokenControllerTests()
    {
        _mockServiceTokenService = new Mock<IServiceTokenService>();
        _controller = new TokenController(_mockServiceTokenService.Object);
    }

    [Fact]
    public async Task ClientCredentials_WithValidCredentials_ReturnsOkWithToken()
    {
        // Arrange
        var clientId = "test-client-id";
        var clientSecret = "test-client-secret";
        var token = "generated_access_token";
        var request = new ClientCredentialsRequest("client_credentials", clientId, clientSecret);

        _mockServiceTokenService.Setup(x => x.AuthenticateClient(clientId, clientSecret))
            .ReturnsAsync(token);

        // Act
        var result = await _controller.ClientCredentials(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult?.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task ClientCredentials_WithInvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var request = new ClientCredentialsRequest("client_credentials", "invalid-id", "invalid-secret");

        _mockServiceTokenService.Setup(x => x.AuthenticateClient(request.ClientId, request.ClientSecret))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _controller.ClientCredentials(request);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task ClientCredentials_WithUnsupportedGrantType_ReturnsBadRequest()
    {
        // Arrange
        var request = new ClientCredentialsRequest("unsupported_grant_type", "test-id", "test-secret");

        // Act
        var result = await _controller.ClientCredentials(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ClientCredentials_WithValidCredentials_ReturnsCorrectTokenType()
    {
        // Arrange
        var token = "access_token_value";
        var request = new ClientCredentialsRequest("client_credentials", "client-id", "client-secret");

        _mockServiceTokenService.Setup(x => x.AuthenticateClient(request.ClientId, request.ClientSecret))
            .ReturnsAsync(token);

        // Act
        var result = await _controller.ClientCredentials(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var response = okResult?.Value as ServiceTokenResponse;
        response?.TokenType.Should().Be("Bearer");
        response?.ExpiresIn.Should().Be(3600);
    }

    [Fact]
    public async Task ClientCredentials_VerifiesClientAuthenticationCalled()
    {
        // Arrange
        var clientId = "test-id";
        var clientSecret = "test-secret";
        var request = new ClientCredentialsRequest("client_credentials", clientId, clientSecret);

        _mockServiceTokenService.Setup(x => x.AuthenticateClient(clientId, clientSecret))
            .ReturnsAsync("token");

        // Act
        await _controller.ClientCredentials(request);

        // Assert
        _mockServiceTokenService.Verify(x => x.AuthenticateClient(clientId, clientSecret), Times.Once);
    }
}

