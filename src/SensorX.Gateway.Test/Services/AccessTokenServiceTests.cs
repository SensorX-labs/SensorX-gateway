using FluentAssertions;
using Moq;
using SensorX.Gateway.Application.Interfaces;
using SensorX.Gateway.Domain.Interfaces;
using SensorX.Gateway.Infrastructure.Services;
using System.Security.Claims;
using Xunit;

namespace SensorX.Gateway.Test.Services;

public class AccessTokenServiceTests
{
    private readonly Mock<IJwtService> _mockJwtService;
    private readonly AccessTokenService _service;

    public AccessTokenServiceTests()
    {
        _mockJwtService = new Mock<IJwtService>();
        _service = new AccessTokenService(_mockJwtService.Object);
    }

    [Fact]
    public void CreateToken_ShouldBuildCorrectClaims()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "john.doe@example.com";
        var role = "admin";
        var scope = "api.read api.write";
        var expectedToken = "signed_jwt_token";

        IEnumerable<Claim>? capturedClaims = null;
        _mockJwtService.Setup(x => x.Sign(It.IsAny<IEnumerable<Claim>>(), It.IsAny<int?>()))
            .Callback<IEnumerable<Claim>, int?>((claims, _) => capturedClaims = claims)
            .Returns(expectedToken);

        // Act
        var result = _service.CreateToken(userId, email, role, scope);

        // Assert
        result.Should().Be(expectedToken);
        capturedClaims.Should().NotBeNull();
        
        var claimsDict = capturedClaims!.ToDictionary(c => c.Type, c => c.Value);
        
        claimsDict["sub"].Should().Be(userId.ToString());
        claimsDict["name"].Should().Be("john.doe");
        claimsDict["unique_name"].Should().Be("john.doe");
        claimsDict["email"].Should().Be(email);
        claimsDict["role"].Should().Be(role);
        claimsDict["scope"].Should().Be(scope);
    }
}
