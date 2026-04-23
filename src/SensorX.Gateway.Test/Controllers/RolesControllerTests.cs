using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SensorX.Gateway.Api.Controllers;
using SensorX.Gateway.Application.Commons.Responses;
using SensorX.Gateway.Application.DTOs;
using SensorX.Gateway.Application.Interfaces;
using SensorX.Gateway.Domain.Enums;
using Xunit;

namespace SensorX.Gateway.Test.Controllers
{
    public class RolesControllerTests
    {
        private readonly Mock<IRoleService> _mockRoleService;
        private readonly RolesController _controller;

        public RolesControllerTests()
        {
            _mockRoleService = new Mock<IRoleService>();
            _controller = new RolesController(_mockRoleService.Object);
        }

        [Fact]
        public void GetAllRoles_ShouldReturnOk()
        {
            var roles = new List<RoleResponse>
            {
                new RoleResponse(1, "Admin"),
                new RoleResponse(2, "SaleStaff")
            };
            _mockRoleService.Setup(x => x.GetAllRoles())
                .Returns(ApiResponse<IEnumerable<RoleResponse>>.SuccessResponse(roles));

            var result = _controller.GetAllRoles() as OkObjectResult;

            result.Should().NotBeNull();
            result!.StatusCode.Should().Be(200);
        }

        [Fact]
        public async Task GetUserRole_WhenExists_ShouldReturnOk()
        {
            var userId = Guid.NewGuid();
            var roleResponse = new RoleResponse(2, "SaleStaff");
            _mockRoleService.Setup(x => x.GetUserRoleAsync(userId))
                .ReturnsAsync(ApiResponse<RoleResponse>.SuccessResponse(roleResponse));

            var result = await _controller.GetUserRole(userId) as OkObjectResult;

            result.Should().NotBeNull();
            result!.StatusCode.Should().Be(200);
        }

        [Fact]
        public async Task GetUserRole_WhenNotExists_ShouldReturnNotFound()
        {
            var userId = Guid.NewGuid();
            _mockRoleService.Setup(x => x.GetUserRoleAsync(userId))
                .ReturnsAsync(ApiResponse<RoleResponse>.FailResponse("Not Found"));

            var result = await _controller.GetUserRole(userId);

            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task AssignRole_WhenSuccess_ShouldReturnOk()
        {
            var request = new AssignRoleRequest(Guid.NewGuid(), Role.SaleStaff);
            _mockRoleService.Setup(x => x.AssignRoleToUserAsync(request))
                .ReturnsAsync(ApiResponse.SuccessResponse("OK"));

            var result = await _controller.AssignRole(request) as OkObjectResult;

            result.Should().NotBeNull();
            result!.StatusCode.Should().Be(200);
        }

        [Fact]
        public async Task AssignRole_WhenFail_ShouldReturnBadRequest()
        {
            var request = new AssignRoleRequest(Guid.NewGuid(), Role.SaleStaff);
            _mockRoleService.Setup(x => x.AssignRoleToUserAsync(request))
                .ReturnsAsync(ApiResponse.FailResponse("Error"));

            var result = await _controller.AssignRole(request);

            result.Should().BeOfType<BadRequestObjectResult>();
        }
    }
}