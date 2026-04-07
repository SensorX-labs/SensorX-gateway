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

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _mockAuthService;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _mockAuthService = new Mock<IAuthService>();
        _controller = new AuthController(_mockAuthService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public async Task Login_WhenSuccess_ReturnsOk()
    {
        var request = new LoginRequest("test@test.com", "pass");
        _mockAuthService.Setup(x => x.LoginAsync(request))
            .ReturnsAsync(ApiResponse<TokenPairResponse>.SuccessResponse(null));

        var result = await _controller.Login(request);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Login_WhenFail_ReturnsUnauthorized()
    {
        var request = new LoginRequest("test@test.com", "pass");
        _mockAuthService.Setup(x => x.LoginAsync(request))
            .ReturnsAsync(ApiResponse<TokenPairResponse>.FailResponse("Error"));

        var result = await _controller.Login(request);
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Refresh_WhenFail_ReturnsUnauthorized()
    {
        var request = new RefreshRequest("token");
        _mockAuthService.Setup(x => x.RefreshAsync(request))
            .ReturnsAsync(ApiResponse<TokenPairResponse>.FailResponse("Error"));

        var result = await _controller.Refresh(request);
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Register_WhenFail_ReturnsConflict()
    {
        var request = new RegisterRequest("test", "test");
        _mockAuthService.Setup(x => x.RegisterAsync(request))
            .ReturnsAsync(ApiResponse<object>.FailResponse("Error"));

        var result = await _controller.Register(request);
        result.Should().BeOfType<ConflictObjectResult>();
    }
}
