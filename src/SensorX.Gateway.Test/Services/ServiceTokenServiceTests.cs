using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using SensorX.Gateway.Application.Interfaces;
using SensorX.Gateway.Domain.Interfaces;
using SensorX.Gateway.Domain.Entities;
using SensorX.Gateway.Infrastructure.Persistence;
using SensorX.Gateway.Infrastructure.Services;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace SensorX.Gateway.Test.Services;

public class ServiceTokenServiceTests
{
    private readonly AppDbContext _dbContext;
    private readonly Mock<IJwtService> _mockJwtService;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly string _hmacSecret = "test-hmac-secret-key-1234567890";
    private readonly ServiceTokenService _service;

    public ServiceTokenServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);

        _mockJwtService = new Mock<IJwtService>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockConfiguration.Setup(x => x["JwtSettings:HmacSecret"]).Returns(_hmacSecret);

        _service = new ServiceTokenService(_dbContext, _mockJwtService.Object, _mockConfiguration.Object);
    }

    [Fact]
    public async Task AuthenticateClient_WithValidCredentials_ShouldReturnTokenWithCorrectClaims()
    {
        // Arrange
        var clientId = "internal-service-1";
        var clientSecret = "secret-abc";
        var secretHash = ComputeHmac(clientSecret);
        var scope = "warehouse.read devices.all";
        var expectedToken = "service_jwt_token";

        var serviceClient = new ServiceClient
        {
            ClientId = clientId,
            ClientSecretHash = secretHash,
            Name = "Internal Service",
            Scope = scope,
            IsActive = true
        };
        _dbContext.ServiceClients.Add(serviceClient);
        await _dbContext.SaveChangesAsync();

        IEnumerable<Claim> capturedClaims = null;
        _mockJwtService.Setup(x => x.Sign(It.IsAny<IEnumerable<Claim>>(), 60))
            .Callback<IEnumerable<Claim>, int?>((claims, _) => capturedClaims = claims)
            .Returns(expectedToken);

        // Act
        var result = await _service.AuthenticateClient(clientId, clientSecret);

        // Assert
        result.Should().Be(expectedToken);
        capturedClaims.Should().NotBeNull();
        
        var claimsDict = capturedClaims.ToDictionary(c => c.Type, c => c.Value);
        claimsDict["sub"].Should().Be(clientId);
        claimsDict["aud"].Should().Be("internal");
        claimsDict["scope"].Should().Be(scope);
    }

    [Fact]
    public async Task AuthenticateClient_WithInvalidSecret_ShouldReturnNull()
    {
        // Arrange
        var clientId = "test-client";
        _dbContext.ServiceClients.Add(new ServiceClient
        {
            ClientId = clientId,
            ClientSecretHash = "some-hash",
            Name = "Test Client",
            Scope = "api",
            IsActive = true
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.AuthenticateClient(clientId, "wrong-secret");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateClient_WithInactiveClient_ShouldReturnNull()
    {
        // Arrange
        var clientId = "inactive-client";
        _dbContext.ServiceClients.Add(new ServiceClient
        {
            ClientId = clientId,
            ClientSecretHash = "hash",
            Name = "Inactive Client",
            Scope = "api",
            IsActive = false
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.AuthenticateClient(clientId, "secret");

        // Assert
        result.Should().BeNull();
    }

    private string ComputeHmac(string value)
    {
        var keyBytes = Encoding.UTF8.GetBytes(_hmacSecret);
        var valueBytes = Encoding.UTF8.GetBytes(value);
        var hash = HMACSHA256.HashData(keyBytes, valueBytes);
        return Convert.ToBase64String(hash);
    }
}
