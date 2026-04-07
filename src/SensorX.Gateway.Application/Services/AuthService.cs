using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SensorX.Gateway.Application.Commons.Responses;
using SensorX.Gateway.Application.DTOs;
using SensorX.Gateway.Application.Interfaces;
using SensorX.Gateway.Domain.Entities;
using SensorX.Gateway.Domain.Interfaces;
using SensorX.Gateway.Domain.Interfaces.Repositories;

namespace SensorX.Gateway.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtService _jwtService;
    private readonly IAccessTokenService _accessTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IRedisPermissionService _permissionService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IUnitOfWork unitOfWork,
        IJwtService jwtService,
        IAccessTokenService accessTokenService,
        IRefreshTokenService refreshTokenService,
        IRedisPermissionService permissionService,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _unitOfWork = unitOfWork;
        _jwtService = jwtService;
        _accessTokenService = accessTokenService;
        _refreshTokenService = refreshTokenService;
        _permissionService = permissionService;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ApiResponse<TokenPairResponse>> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user == null)
            return ApiResponse<TokenPairResponse>.FailResponse("Invalid credentials");

        if (user.IsLocked && user.LockedUntil > DateTimeOffset.UtcNow)
        {
            var msg = $"Account locked until {user.LockedUntil}";
            return ApiResponse<TokenPairResponse>.FailResponse(msg);
        }

        if (user.IsLocked && user.LockedUntil <= DateTimeOffset.UtcNow)
        {
            user.ResetLoginFailures();
        }

        if (!await _passwordHasher.VerifyAsync(request.Password, user.PasswordHash))
        {
            var maxAttempts = _configuration.GetValue<int>("Security:MaxLoginAttempts", 5);
            var lockoutMinutes = 15 * (int)Math.Pow(2, user.LockCount);
            
            user.RecordFailedLogin(maxAttempts, lockoutMinutes);
            
            if (user.IsLocked)
            {
                _logger.LogWarning("Account locked for {Email} after {Attempts} failed attempts", user.Email, user.LoginFailCount);
            }
            await _unitOfWork.SaveChangesAsync();
            return ApiResponse<TokenPairResponse>.FailResponse("Invalid credentials");
        }

        var response = await IssueTokenPairAsync(user);
        return ApiResponse<TokenPairResponse>.SuccessResponse(response);
    }

    public async Task<ApiResponse<TokenPairResponse>> RefreshAsync(RefreshRequest request)
    {
        try
        {
            var (userId, newRawToken) = await _refreshTokenService.RefreshAsync(request.RefreshToken);
            var user = await _userRepository.GetByIdAsync(userId);

            if (user == null || user.IsLocked)
                return ApiResponse<TokenPairResponse>.FailResponse("Account is locked or invalid");

            var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
            await _permissionService.SetPermissionsAsync(userId, roles);

            var accessToken = _accessTokenService.CreateToken(
                user.Id, user.Email,
                string.Join(",", roles), string.Join(" ", roles));

            var result = new TokenPairResponse(accessToken, newRawToken, new UserInfoResponse(user.Id, user.Email, roles));
            return ApiResponse<TokenPairResponse>.SuccessResponse(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Refresh token failure: {Reason}", ex.Message);
            return ApiResponse<TokenPairResponse>.FailResponse(ex.Message);
        }
    }

    public async Task<ApiResponse> LogoutAsync(string? userIdString, LogoutRequest request)
    {
        await _refreshTokenService.RevokeAsync(request.RefreshToken);

        if (userIdString != null && Guid.TryParse(userIdString, out var userId))
        {
            await _permissionService.RemovePermissionsAsync(userId);
        }

        return ApiResponse.SuccessResponse("Logged out");
    }

    public async Task<ApiResponse<object>> RegisterAsync(RegisterRequest request)
    {
        if (await _userRepository.AnyByEmailAsync(request.Email))
            return ApiResponse<object>.FailResponse("Email already registered");

        var passwordHash = await _passwordHasher.HashAsync(request.Password);
        var user = User.Create(request.Email, passwordHash);
        
        _userRepository.Add(user);

        var defaultRole = await _roleRepository.GetByNameAsync("user");
        if (defaultRole != null)
        {
            user.AddRole(defaultRole.Id);
        }

        await _unitOfWork.SaveChangesAsync();
        return ApiResponse<object>.SuccessResponse(new { userId = user.Id }, "User registered");
    }

    public ApiResponse<IntrospectResponse> Introspect(IntrospectRequest request)
    {
        var principal = _jwtService.ValidateToken(request.Token);
        if (principal == null)
            return ApiResponse<IntrospectResponse>.SuccessResponse(new IntrospectResponse(false));

        var resp = new IntrospectResponse(
            Active: true,
            Sub: principal.FindFirst("sub")?.Value,
            Scope: principal.FindFirst("scope")?.Value,
            Exp: principal.FindFirst("exp")?.Value);

        return ApiResponse<IntrospectResponse>.SuccessResponse(resp);
    }

    public async Task<ApiResponse> RevokeAsync(string? userIdString)
    {
        if (userIdString != null && Guid.TryParse(userIdString, out var userId))
        {
            await _refreshTokenService.RevokeAllForUserAsync(userId);
            await _permissionService.RemovePermissionsAsync(userId);
        }
        return ApiResponse.SuccessResponse("All tokens revoked");
    }

    public async Task<ApiResponse> ChangePasswordAsync(string? userIdString, ChangePasswordRequest request)
    {
        if (userIdString == null || !Guid.TryParse(userIdString, out var userId))
            return ApiResponse.FailResponse("Unauthorized");

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return ApiResponse.FailResponse("Unauthorized");

        if (!await _passwordHasher.VerifyAsync(request.OldPassword, user.PasswordHash))
            return ApiResponse.FailResponse("Incorrect old password");

        var newPasswordHash = await _passwordHasher.HashAsync(request.NewPassword);
        user.ChangePassword(newPasswordHash);
        
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse.SuccessResponse("Password changed successfully");
    }

    private async Task<TokenPairResponse> IssueTokenPairAsync(User user)
    {
        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        
        user.ResetLoginFailures();
        await _unitOfWork.SaveChangesAsync();

        await _permissionService.SetPermissionsAsync(user.Id, roles);

        var accessToken = _accessTokenService.CreateToken(
            user.Id, user.Email,
            string.Join(",", roles), string.Join(" ", roles));

        var refreshToken = await _refreshTokenService.CreateAsync(user.Id);

        return new TokenPairResponse(accessToken, refreshToken,
            new UserInfoResponse(user.Id, user.Email, roles));
    }
}
