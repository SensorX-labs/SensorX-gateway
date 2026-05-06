using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SensorX.Gateway.Application.Commons.Responses;
using SensorX.Gateway.Application.DTOs;
using SensorX.Gateway.Application.Interfaces;
using SensorX.Gateway.Domain.Entities;
using SensorX.Gateway.Domain.Enums;
using SensorX.Gateway.Domain.Interfaces;
using SensorX.Gateway.Domain.Interfaces.Repositories;

namespace SensorX.Gateway.Application.Services;

public class AuthService(
    IAccountRepository _accountRepository,
    IUnitOfWork _unitOfWork,
    IJwtService _jwtService,
    IAccessTokenService _accessTokenService,
    IRefreshTokenService _refreshTokenService,
    IRedisPermissionService _permissionService,
    IPasswordHasher _passwordHasher,
    IConfiguration _configuration,
    ILogger<AuthService> _logger
) : IAuthService
{
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

    public async Task<ApiResponse<IEnumerable<UserResponse>>> GetAllUsersAsync()
    {
        var accounts = await _accountRepository.GetAllAsync();
        var users = accounts.Select(a => new UserResponse(
            a.Id,
            a.Email,
            a.FullName,
            a.Role.ToString(),
            a.IsLocked,
            a.CreatedAt));
        return ApiResponse<IEnumerable<UserResponse>>.SuccessResponse(users);
    }

    public async Task<ApiResponse> ToggleUserLockAsync(Guid userId)
    {
        var account = await _accountRepository.GetByIdAsync(userId);
        if (account == null)
            return ApiResponse.FailResponse("Account not found");

        account.ToggleLock();
        await _unitOfWork.SaveChangesAsync();

        var status = account.IsLocked ? "locked" : "unlocked";
        _logger.LogInformation("Account {Email} ({AccountId}) has been {Status}",
            account.Email, account.Id, status);

        return ApiResponse.SuccessResponse($"Account {status} successfully");
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
