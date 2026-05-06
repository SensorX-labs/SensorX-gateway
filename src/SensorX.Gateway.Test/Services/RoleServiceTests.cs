using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SensorX.Gateway.Application.DTOs;
using SensorX.Gateway.Application.Services;
using SensorX.Gateway.Domain.Entities;
using SensorX.Gateway.Domain.Enums;
using SensorX.Gateway.Domain.Interfaces.Repositories;
using Xunit;

namespace SensorX.Gateway.Test.Services;

public class RoleServiceTests
{
    private readonly Mock<IAccountRepository> _mockAccountRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<RoleService>> _mockLogger;
    private readonly RoleService _roleService;

    public RoleServiceTests()
    {
        _mockAccountRepository = new Mock<IAccountRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<RoleService>>();
        _roleService = new RoleService(
            _mockAccountRepository.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);
    }

    [Fact]
    public void GetAllRoles_ShouldReturnAllEnumValues()
    {
        // Act
        var result = _roleService.GetAllRoles();

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetUserRoleAsync_WhenUserExists_ShouldReturnRole()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var account = Account.Create("test@test.com", "Name", "hash", Role.Admin);
        _mockAccountRepository.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(account);

        // Act
        var result = await _roleService.GetUserRoleAsync(userId);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Name.Should().Be("Admin");
    }

    [Fact]
    public async Task AssignRoleToUserAsync_WhenValidRequest_ShouldAssignRole()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var account = Account.Create("test@test.com", "Name", "hash", Role.WarehouseStaff);

        _mockAccountRepository.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(account);

        // Act
        var result = await _roleService.AssignRoleToUserAsync(new AssignRoleRequest(userId, Role.SaleStaff));

        // Assert
        result.Success.Should().BeTrue();
        account.Role.Should().Be(Role.SaleStaff);
        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}