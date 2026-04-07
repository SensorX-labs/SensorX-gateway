using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SensorX.Gateway.Application.DTOs;
using SensorX.Gateway.Application.Interfaces;
using SensorX.Gateway.Application.Services;
using SensorX.Gateway.Domain.Entities;
using SensorX.Gateway.Domain.Interfaces.Repositories;
using Xunit;

namespace SensorX.Gateway.Test.Services;

public class RoleServiceTests
{
    private readonly Mock<IRoleRepository> _mockRoleRepository;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<RoleService>> _mockLogger;
    private readonly RoleService _roleService;

    public RoleServiceTests()
    {
        _mockRoleRepository = new Mock<IRoleRepository>();
        _mockUserRepository = new Mock<IUserRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<RoleService>>();
        _roleService = new RoleService(
            _mockRoleRepository.Object,
            _mockUserRepository.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);
    }

    #region GetAllRolesAsync Tests

    [Fact]
    public async Task GetAllRolesAsync_WhenRolesExist_ShouldReturnAllRoles()
    {
        // Arrange
        var roles = new List<Role>
        {
            new Role { Id = Guid.NewGuid(), Name = "admin" },
            new Role { Id = Guid.NewGuid(), Name = "user" }
        };
        _mockRoleRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(roles);

        // Act
        var result = await _roleService.GetAllRolesAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllRolesAsync_WhenNoRoles_ShouldReturnEmptyList()
    {
        // Arrange
        _mockRoleRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Role>());

        // Act
        var result = await _roleService.GetAllRolesAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }

    #endregion

    #region GetRoleByIdAsync Tests

    [Fact]
    public async Task GetRoleByIdAsync_WhenRoleExists_ShouldReturnRole()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var role = new Role { Id = roleId, Name = "admin" };
        _mockRoleRepository.Setup(x => x.GetByIdAsync(roleId)).ReturnsAsync(role);

        // Act
        var result = await _roleService.GetRoleByIdAsync(roleId);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Id.Should().Be(roleId);
    }

    [Fact]
    public async Task GetRoleByIdAsync_WhenRoleNotFound_ShouldReturnFailResponse()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        _mockRoleRepository.Setup(x => x.GetByIdAsync(roleId)).ReturnsAsync((Role?)null);

        // Act
        var result = await _roleService.GetRoleByIdAsync(roleId);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Role not found");
    }

    #endregion

    #region CreateRoleAsync Tests

    [Fact]
    public async Task CreateRoleAsync_WhenValidRequest_ShouldCreateRole()
    {
        // Arrange
        var request = new CreateRoleRequest("new_role");
        _mockRoleRepository.Setup(x => x.GetByNameAsync("new_role")).ReturnsAsync((Role?)null);

        // Act
        var result = await _roleService.CreateRoleAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Name.Should().Be("new_role");
        _mockRoleRepository.Verify(x => x.Add(It.IsAny<Role>()), Times.Once);
        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateRoleAsync_WhenRoleExists_ShouldReturnFailResponse()
    {
        // Arrange
        var request = new CreateRoleRequest("existing_role");
        _mockRoleRepository.Setup(x => x.GetByNameAsync("existing_role"))
            .ReturnsAsync(new Role { Id = Guid.NewGuid(), Name = "existing_role" });

        // Act
        var result = await _roleService.CreateRoleAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Role already exists");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateRoleAsync_WhenEmptyName_ShouldReturnFailResponse(string name)
    {
        // Arrange
        var request = new CreateRoleRequest(name);

        // Act
        var result = await _roleService.CreateRoleAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Role name is required");
    }

    #endregion

    #region UpdateRoleAsync Tests

    [Fact]
    public async Task UpdateRoleAsync_WhenRoleExists_ShouldUpdateRole()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var role = new Role { Id = roleId, Name = "old_name" };
        var request = new UpdateRoleRequest("new_name");
        _mockRoleRepository.Setup(x => x.GetByIdAsync(roleId)).ReturnsAsync(role);
        _mockRoleRepository.Setup(x => x.GetByNameAsync("new_name")).ReturnsAsync((Role?)null);

        // Act
        var result = await _roleService.UpdateRoleAsync(roleId, request);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Name.Should().Be("new_name");
        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateRoleAsync_WhenRoleNotFound_ShouldReturnFailResponse()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var request = new UpdateRoleRequest("new_name");
        _mockRoleRepository.Setup(x => x.GetByIdAsync(roleId)).ReturnsAsync((Role?)null);

        // Act
        var result = await _roleService.UpdateRoleAsync(roleId, request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Role not found");
    }

    [Fact]
    public async Task UpdateRoleAsync_WhenNameExists_ShouldReturnFailResponse()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var otherRoleId = Guid.NewGuid();
        var role = new Role { Id = roleId, Name = "old_name" };
        var existingRole = new Role { Id = otherRoleId, Name = "new_name" };
        var request = new UpdateRoleRequest("new_name");
        _mockRoleRepository.Setup(x => x.GetByIdAsync(roleId)).ReturnsAsync(role);
        _mockRoleRepository.Setup(x => x.GetByNameAsync("new_name")).ReturnsAsync(existingRole);

        // Act
        var result = await _roleService.UpdateRoleAsync(roleId, request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Role name already exists");
    }

    #endregion

    #region DeleteRoleAsync Tests

    [Fact]
    public async Task DeleteRoleAsync_WhenRoleHasNoUsers_ShouldDeleteRole()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var role = new Role { Id = roleId, Name = "unused_role", UserRoles = new List<UserRole>() };
        _mockRoleRepository.Setup(x => x.GetByIdAsync(roleId)).ReturnsAsync(role);

        // Act
        var result = await _roleService.DeleteRoleAsync(roleId);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Role deleted successfully");
        _mockRoleRepository.Verify(x => x.Remove(role), Times.Once);
        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteRoleAsync_WhenRoleHasUsers_ShouldReturnFailResponse()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var role = new Role
        {
            Id = roleId,
            Name = "used_role",
            UserRoles = new List<UserRole>
            {
                new UserRole { UserId = Guid.NewGuid(), RoleId = roleId }
            }
        };
        _mockRoleRepository.Setup(x => x.GetByIdAsync(roleId)).ReturnsAsync(role);

        // Act
        var result = await _roleService.DeleteRoleAsync(roleId);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Cannot delete role that is assigned to users");
    }

    [Fact]
    public async Task DeleteRoleAsync_WhenRoleNotFound_ShouldReturnFailResponse()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        _mockRoleRepository.Setup(x => x.GetByIdAsync(roleId)).ReturnsAsync((Role?)null);

        // Act
        var result = await _roleService.DeleteRoleAsync(roleId);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Role not found");
    }

    #endregion

    #region AssignRoleToUserAsync Tests

    [Fact]
    public async Task AssignRoleToUserAsync_WhenValidRequest_ShouldAssignRole()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var user = User.Create("test@test.com", "hash");
        var role = new Role { Id = roleId, Name = "user" };

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);
        _mockRoleRepository.Setup(x => x.GetByIdAsync(roleId)).ReturnsAsync(role);

        // Act
        var result = await _roleService.AssignRoleToUserAsync(new AssignRoleRequest(userId, roleId));

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Role assigned to user successfully");
        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignRoleToUserAsync_WhenUserNotFound_ShouldReturnFailResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        _mockUserRepository.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync((User?)null);

        // Act
        var result = await _roleService.AssignRoleToUserAsync(new AssignRoleRequest(userId, roleId));

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("User not found");
    }

    [Fact]
    public async Task AssignRoleToUserAsync_WhenRoleNotFound_ShouldReturnFailResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var user = User.Create("test@test.com", "hash");
        _mockUserRepository.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);
        _mockRoleRepository.Setup(x => x.GetByIdAsync(roleId)).ReturnsAsync((Role?)null);

        // Act
        var result = await _roleService.AssignRoleToUserAsync(new AssignRoleRequest(userId, roleId));

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Role not found");
    }

    [Fact]
    public async Task AssignRoleToUserAsync_WhenRoleAlreadyAssigned_ShouldReturnFailResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var user = User.Create("test@test.com", "hash");
        user.AddRole(roleId);
        var role = new Role { Id = roleId, Name = "user" };

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);
        _mockRoleRepository.Setup(x => x.GetByIdAsync(roleId)).ReturnsAsync(role);

        // Act
        var result = await _roleService.AssignRoleToUserAsync(new AssignRoleRequest(userId, roleId));

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Role is already assigned to user");
    }

    #endregion

    #region GetUserRolesAsync Tests

    [Fact]
    public async Task GetUserRolesAsync_WhenUserExists_ShouldReturnRoles()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = User.Create("test@test.com", "hash");
        var roleId = Guid.NewGuid();
        user.AddRole(roleId);

        var roles = new List<Role> { new Role { Id = roleId, Name = "user" } };
        _mockUserRepository.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);
        _mockRoleRepository.Setup(x => x.GetUserRolesAsync(userId)).ReturnsAsync(roles);

        // Act
        var result = await _roleService.GetUserRolesAsync(userId);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetUserRolesAsync_WhenUserNotFound_ShouldReturnFailResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockUserRepository.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync((User?)null);

        // Act
        var result = await _roleService.GetUserRolesAsync(userId);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("User not found");
    }

    #endregion

    #region RemoveRoleFromUserAsync Tests

    [Fact]
    public async Task RemoveRoleFromUserAsync_WhenRoleAssigned_ShouldRemoveRole()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var user = User.Create("test@test.com", "hash");
        user.AddRole(roleId);
        var role = new Role { Id = roleId, Name = "user" };

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);
        _mockRoleRepository.Setup(x => x.GetByIdAsync(roleId)).ReturnsAsync(role);

        // Act
        var result = await _roleService.RemoveRoleFromUserAsync(userId, roleId);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Role removed from user successfully");
        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveRoleFromUserAsync_WhenRoleNotAssigned_ShouldReturnFailResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var user = User.Create("test@test.com", "hash");
        var role = new Role { Id = roleId, Name = "user" };

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);
        _mockRoleRepository.Setup(x => x.GetByIdAsync(roleId)).ReturnsAsync(role);

        // Act
        var result = await _roleService.RemoveRoleFromUserAsync(userId, roleId);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Role is not assigned to user");
    }

    #endregion
}