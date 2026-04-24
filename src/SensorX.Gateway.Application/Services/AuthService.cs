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
    private readonly IAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtService _jwtService;
    private readonly IAccessTokenService _accessTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IRedisPermissionService _permissionService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IAccountRepository accountRepository,
        IUnitOfWork unitOfWork,
        IJwtService jwtService,
        IAccessTokenService accessTokenService,
        IRefreshTokenService refreshTokenService,
        IRedisPermissionService permissionService,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _accountRepository = accountRepository;
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
        var account = await _accountRepository.GetByEmailAsync(request.Email);
        if (account == null)
            return ApiResponse<TokenPairResponse>.FailResponse("Invalid credentials");

        if (account.IsLocked && account.LockedUntil > DateTimeOffset.UtcNow)
        {
            var msg = $"Account locked until {account.LockedUntil}";
            return ApiResponse<TokenPairResponse>.FailResponse(msg);
        }

        if (account.IsLocked && account.LockedUntil <= DateTimeOffset.UtcNow)
        {
            account.ResetLoginFailures();
        }

        if (!await _passwordHasher.VerifyAsync(request.Password, account.PasswordHash))
        {
            var maxAttempts = _configuration.GetValue<int>("Security:MaxLoginAttempts", 5);
            var lockoutMinutes = 15 * (int)Math.Pow(2, account.LockCount);
            
            account.RecordFailedLogin(maxAttempts, lockoutMinutes);
            
            if (account.IsLocked)
            {
                _logger.LogWarning("Account locked for {Email} after {Attempts} failed attempts", account.Email, account.LoginFailCount);
            }
            await _unitOfWork.SaveChangesAsync();
            return ApiResponse<TokenPairResponse>.FailResponse("Invalid credentials");
        }

        var response = await IssueTokenPairAsync(account);
        return ApiResponse<TokenPairResponse>.SuccessResponse(response);
    }

    public async Task<ApiResponse<TokenPairResponse>> RefreshAsync(RefreshRequest request)
    {
        try
        {
            var (accountId, newRawToken) = await _refreshTokenService.RefreshAsync(request.RefreshToken);
            var account = await _accountRepository.GetByIdAsync(accountId);

            if (account == null || account.IsLocked)
                return ApiResponse<TokenPairResponse>.FailResponse("Account is locked or invalid");

            var roleStr = account.Role.ToString();
            var roles = new List<string> { roleStr };
            await _permissionService.SetPermissionsAsync(accountId, roles);

            var accessToken = _accessTokenService.CreateToken(
                account.Id, account.Email,
                roleStr, roleStr);

            var result = new TokenPairResponse(accessToken, newRawToken, new UserInfoResponse(account.Id, account.Email, roles));
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

        if (userIdString != null && Guid.TryParse(userIdString, out var accountId))
        {
            await _permissionService.RemovePermissionsAsync(accountId);
        }

        return ApiResponse.SuccessResponse("Logged out");
    }

    public async Task<ApiResponse<object>> RegisterAsync(RegisterRequest request)
    {
        if (await _accountRepository.AnyByEmailAsync(request.Email))
            return ApiResponse<object>.FailResponse("Email already registered");

        var passwordHash = await _passwordHasher.HashAsync(request.Password);
        
        // Split email for a preliminary FullName
        var generatedFullName = request.Email.Split('@')[0];
        
        var account = Account.Create(request.Email, generatedFullName, passwordHash);
        
        _accountRepository.Add(account);

        await _unitOfWork.SaveChangesAsync();
        return ApiResponse<object>.SuccessResponse(new { userId = account.Id }, "Account registered");
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
        if (userIdString != null && Guid.TryParse(userIdString, out var accountId))
        {
            await _refreshTokenService.RevokeAllForUserAsync(accountId);
            await _permissionService.RemovePermissionsAsync(accountId);
        }
        return ApiResponse.SuccessResponse("All tokens revoked");
    }

    public async Task<ApiResponse> ChangePasswordAsync(string? userIdString, ChangePasswordRequest request)
    {
        if (userIdString == null || !Guid.TryParse(userIdString, out var accountId))
            return ApiResponse.FailResponse("Unauthorized");

        var account = await _accountRepository.GetByIdAsync(accountId);
        if (account == null)
            return ApiResponse.FailResponse("Unauthorized");

        if (!await _passwordHasher.VerifyAsync(request.OldPassword, account.PasswordHash))
            return ApiResponse.FailResponse("Incorrect old password");

        var newPasswordHash = await _passwordHasher.HashAsync(request.NewPassword);
        account.ChangePassword(newPasswordHash);
        
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse.SuccessResponse("Password changed successfully");
    }

    private async Task<TokenPairResponse> IssueTokenPairAsync(Account account)
    {
        var roleStr = account.Role.ToString();
        var roles = new List<string> { roleStr };
        
        account.ResetLoginFailures();
        await _unitOfWork.SaveChangesAsync();

        await _permissionService.SetPermissionsAsync(account.Id, roles);

        var accessToken = _accessTokenService.CreateToken(
            account.Id, account.Email,
            roleStr, roleStr);

        var refreshToken = await _refreshTokenService.CreateAsync(account.Id);

        return new TokenPairResponse(accessToken, refreshToken,
            new UserInfoResponse(account.Id, account.Email, roles));
    }
}
