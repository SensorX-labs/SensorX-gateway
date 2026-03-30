using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Moq;
using SensorX.Gateway.Api.Authorization;
using SensorX.Gateway.Domain.Interfaces;
using System.Security.Claims;
using Xunit;

namespace SensorX.Gateway.Test.Authorization;

public class PermissionAuthorizationHandlerTests
{
    private readonly Mock<IRedisPermissionService> _mockPermissionService;
    private readonly PermissionAuthorizationHandler _handler;

    public PermissionAuthorizationHandlerTests()
    {
        _mockPermissionService = new Mock<IRedisPermissionService>();
        _handler = new PermissionAuthorizationHandler(_mockPermissionService.Object);
    }

    [Fact]
    public async Task HandleRequirementAsync_WithSufficientPermission_ShouldSucceed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var permissions = new List<string> { "read:warehouse", "write:warehouse" };
        var requirement = new PermissionRequirement("read:warehouse");
        
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", userId.ToString())
        }));

        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);
        _mockPermissionService.Setup(x => x.GetPermissionsAsync(userId))
            .ReturnsAsync(permissions);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_WithFullPermission_ShouldSucceed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var permissions = new List<string> { "full_permission" };
        var requirement = new PermissionRequirement("any:permission");
        
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", userId.ToString())
        }));

        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);
        _mockPermissionService.Setup(x => x.GetPermissionsAsync(userId))
            .ReturnsAsync(permissions);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_WithoutPermission_ShouldNotSucceed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var permissions = new List<string> { "other:permission" };
        var requirement = new PermissionRequirement("required:permission");
        
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", userId.ToString())
        }));

        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);
        _mockPermissionService.Setup(x => x.GetPermissionsAsync(userId))
            .ReturnsAsync(permissions);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_WithMissingSubClaim_ShouldNotSucceed()
    {
        // Arrange
        var requirement = new PermissionRequirement("read:warehouse");
        var user = new ClaimsPrincipal(new ClaimsIdentity()); // No claims

        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        _mockPermissionService.Verify(x => x.GetPermissionsAsync(It.IsAny<Guid>()), Times.Never);
    }
}
