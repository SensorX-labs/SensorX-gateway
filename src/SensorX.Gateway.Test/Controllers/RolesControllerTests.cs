using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SensorX.Gateway.Api.Controllers;
using SensorX.Gateway.Application.Commons.Responses;
using SensorX.Gateway.Application.DTOs;
using SensorX.Gateway.Application.Interfaces;
using Xunit;

namespace SensorX.Gateway.Test.Controllers;

public class RolesControllerTests
{
    private readonly Mock<IRoleService> _mockRoleService;
    private readonly RolesController _controller;

    public RolesControllerTests()
    {
        _mockRoleService = new Mock<IRoleService>();
        _controller = new RolesController(_mockRoleService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    #region GetAllRoles Tests

    [Fact]
    public async Task GetAllRoles_WhenSuccess_ReturnsOk()
    {
        // Arrange
        var roles = new List<RoleResponse>
        {
            new RoleResponse(Guid.NewGuid(), "admin"),
            new RoleResponse(Guid.NewGuid(), "user")
        };
        _mockRoleService.Setup(x => x.GetAllRolesAsync())
            .ReturnsAsync(ApiResponse<IEnumerable<RoleResponse>>.SuccessResponse(roles));

        // Act
        var result = await _controller.GetAllRoles();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region GetRoleById Tests

    [Fact]
    public async Task GetRoleById_WhenRoleExists_ReturnsOk()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var role = new RoleResponse(roleId, "admin");
        _mockRoleService.Setup(x => x.GetRoleByIdAsync(roleId))
            .ReturnsAsync(ApiResponse<RoleResponse>.SuccessResponse(role));

        // Act
        var result = await _controller.GetRoleById(roleId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetRoleById_WhenRoleNotFound_ReturnsNotFound()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        _mockRoleService.Setup(x => x.GetRoleByIdAsync(roleId))
            .ReturnsAsync(ApiResponse<RoleResponse>.FailResponse("Role not found"));

        // Act
        var result = await _controller.GetRoleById(roleId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region CreateRole Tests

    [Fact]
    public async Task CreateRole_WhenSuccess_ReturnsCreated()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var request = new CreateRoleRequest("new_role");
        var response = new RoleResponse(roleId, "new_role");
        _mockRoleService.Setup(x => x.CreateRoleAsync(request))
            .ReturnsAsync(ApiResponse<RoleResponse>.SuccessResponse(response, "Role created successfully"));

        // Act
        var result = await _controller.CreateRole(request);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = result as CreatedAtActionResult;
        createdResult!.ActionName.Should().Be("GetRoleById");
    }

    [Fact]
    public async Task CreateRole_WhenFail_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateRoleRequest("existing_role");
        _mockRoleService.Setup(x => x.CreateRoleAsync(request))
            .ReturnsAsync(ApiResponse<RoleResponse>.FailResponse("Role already exists"));

        // Act
        var result = await _controller.CreateRole(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region UpdateRole Tests

    [Fact]
    public async Task UpdateRole_WhenSuccess_ReturnsOk()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var request = new UpdateRoleRequest("updated_role");
        var response = new RoleResponse(roleId, "updated_role");
        _mockRoleService.Setup(x => x.UpdateRoleAsync(roleId, request))
            .ReturnsAsync(ApiResponse<RoleResponse>.SuccessResponse(response, "Role updated successfully"));

        // Act
        var result = await _controller.UpdateRole(roleId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateRole_WhenFail_ReturnsBadRequest()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var request = new UpdateRoleRequest("updated_role");
        _mockRoleService.Setup(x => x.UpdateRoleAsync(roleId, request))
            .ReturnsAsync(ApiResponse<RoleResponse>.FailResponse("Role not found"));

        // Act
        var result = await _controller.UpdateRole(roleId, request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region DeleteRole Tests

    [Fact]
    public async Task DeleteRole_WhenSuccess_ReturnsOk()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        _mockRoleService.Setup(x => x.DeleteRoleAsync(roleId))
            .ReturnsAsync(ApiResponse.SuccessResponse("Role deleted successfully"));

        // Act
        var result = await _controller.DeleteRole(roleId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DeleteRole_WhenFail_ReturnsBadRequest()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        _mockRoleService.Setup(x => x.DeleteRoleAsync(roleId))
            .ReturnsAsync(ApiResponse.FailResponse("Role not found"));

        // Act
        var result = await _controller.DeleteRole(roleId);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region AssignRole Tests

    [Fact]
    public async Task AssignRole_WhenSuccess_ReturnsOk()
    {
        // Arrange
        var request = new AssignRoleRequest(Guid.NewGuid(), Guid.NewGuid());
        _mockRoleService.Setup(x => x.AssignRoleToUserAsync(request))
            .ReturnsAsync(ApiResponse.SuccessResponse("Role assigned to user successfully"));

        // Act
        var result = await _controller.AssignRole(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AssignRole_WhenFail_ReturnsBadRequest()
    {
        // Arrange
        var request = new AssignRoleRequest(Guid.NewGuid(), Guid.NewGuid());
        _mockRoleService.Setup(x => x.AssignRoleToUserAsync(request))
            .ReturnsAsync(ApiResponse.FailResponse("User not found"));

        // Act
        var result = await _controller.AssignRole(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region RemoveRoleFromUser Tests

    [Fact]
    public async Task RemoveRoleFromUser_WhenSuccess_ReturnsOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        _mockRoleService.Setup(x => x.RemoveRoleFromUserAsync(userId, roleId))
            .ReturnsAsync(ApiResponse.SuccessResponse("Role removed from user successfully"));

        // Act
        var result = await _controller.RemoveRoleFromUser(userId, roleId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RemoveRoleFromUser_WhenFail_ReturnsBadRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        _mockRoleService.Setup(x => x.RemoveRoleFromUserAsync(userId, roleId))
            .ReturnsAsync(ApiResponse.FailResponse("Role is not assigned to user"));

        // Act
        var result = await _controller.RemoveRoleFromUser(userId, roleId);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion
}