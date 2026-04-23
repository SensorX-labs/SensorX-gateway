using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SensorX.Gateway.Api.Controllers;
using SensorX.Gateway.Application.Commons.Responses;
using SensorX.Gateway.Application.DTOs;
using SensorX.Gateway.Application.Interfaces;
using Xunit;
using System.Security.Claims;

namespace SensorX.Gateway.Test.Controllers
{
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
            var request = new RegisterRequest("test@test.com", "password");
            _mockAuthService.Setup(x => x.RegisterAsync(request))
                .ReturnsAsync(ApiResponse<object>.FailResponse("Error"));

            var result = await _controller.Register(request);
            result.Should().BeOfType<ConflictObjectResult>();
        }

        [Fact]
        public async Task Logout_WhenSuccess_ReturnsOk()
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim("sub", "userId") }));
            _controller.ControllerContext.HttpContext.User = user;
            
            var request = new LogoutRequest("refresh-token");
            _mockAuthService.Setup(x => x.LogoutAsync("userId", request))
                .ReturnsAsync(ApiResponse.SuccessResponse("Logged out"));

            var result = await _controller.Logout(request);
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public void Introspect_WhenSuccess_ReturnsOk()
        {
            var request = new IntrospectRequest("token");
            _mockAuthService.Setup(x => x.Introspect(request))
                .Returns(ApiResponse<IntrospectResponse>.SuccessResponse(new IntrospectResponse(true)));

            var result = _controller.Introspect(request);
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task Register_Success_ReturnsCreated()
        {
            var request = new RegisterRequest("username", "password");
            _mockAuthService.Setup(x => x.RegisterAsync(request))
                .ReturnsAsync(ApiResponse<object>.SuccessResponse("User registered"));

            var result = await _controller.Register(request);
            result.Should().BeOfType<CreatedResult>();
        }
    }
}