using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
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
    IEmailSender _emailSender,
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
                roleStr, roleStr, account.WarehouseId);

            var result = new TokenPairResponse(accessToken, newRawToken, new UserInfoResponse(account.Id, account.Email, roles, account.FullName, account.AvatarUrl, account.WarehouseId));
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

        return ApiResponse.SuccessResponse("Đã đăng xuất thành công");
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

    public async Task<ApiResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return ApiResponse.FailResponse("Email is required.");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var genericMessage = "Nếu email tồn tại, hệ thống đã gửi mật khẩu mới tới hộp thư của bạn.";
        var account = await _accountRepository.GetByEmailAsync(normalizedEmail);

        if (account == null)
        {
            _logger.LogInformation("Forgot password requested for non-existing email {Email}", normalizedEmail);
            return ApiResponse.SuccessResponse(genericMessage);
        }

        var temporaryPassword = GenerateTemporaryPassword();

        try
        {
            await _emailSender.SendAsync(
                account.Email,
                "SensorX - Mật khẩu tạm thời mới",
                BuildForgotPasswordEmailBody(account.FullName, temporaryPassword));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send forgot password email to {Email}", account.Email);
            return ApiResponse.FailResponse("Không thể gửi email lúc này. Vui lòng thử lại sau.");
        }

        var passwordHash = await _passwordHasher.HashAsync(temporaryPassword);
        account.ChangePassword(passwordHash);
        await _unitOfWork.SaveChangesAsync();
        await _refreshTokenService.RevokeAllForUserAsync(account.Id);

        return ApiResponse.SuccessResponse(genericMessage);
    }

    public async Task<ApiResponse> ChangePasswordAsync(string? userIdString, ChangePasswordRequest request)
    {
        if (userIdString == null || !Guid.TryParse(userIdString, out var accountId))
            return ApiResponse.FailResponse("Bạn chưa đăng nhập hoặc phiên không hợp lệ.");

        var account = await _accountRepository.GetByIdAsync(accountId);
        if (account == null)
            return ApiResponse.FailResponse("Tài khoản không tồn tại hoặc không hợp lệ.");

        // Kiểm tra độ dài mật khẩu mới
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8 || request.NewPassword.Length > 64)
            return ApiResponse.FailResponse("Mật khẩu mới phải có độ dài từ 8-64 ký tự.");

        // Kiểm tra trùng mật khẩu cũ
        if (request.OldPassword == request.NewPassword)
            return ApiResponse.FailResponse("Mật khẩu mới không được trùng mật khẩu cũ.");

        // Kiểm tra old password
        if (!await _passwordHasher.VerifyAsync(request.OldPassword, account.PasswordHash))
            return ApiResponse.FailResponse("Mật khẩu cũ không chính xác.");

        // Tùy chọn thêm: kiểm tra độ mạnh mật khẩu mới (chữ hoa, chữ thường, số, ký tự đặc biệt)
        var hasUpper = request.NewPassword.Any(char.IsUpper);
        var hasLower = request.NewPassword.Any(char.IsLower);
        var hasDigit = request.NewPassword.Any(char.IsDigit);
        var hasSymbol = request.NewPassword.Any(ch => !char.IsLetterOrDigit(ch));
        if (!(hasUpper && hasLower && hasDigit && hasSymbol))
            return ApiResponse.FailResponse("Mật khẩu mới phải chứa cả chữ hoa, chữ thường, số và ký tự đặc biệt.");

        var newPasswordHash = await _passwordHasher.HashAsync(request.NewPassword);
        account.ChangePassword(newPasswordHash);

        await _unitOfWork.SaveChangesAsync();

        // TODO: revoke all refresh tokens for this user (optionally)

        return ApiResponse.SuccessResponse("Đổi mật khẩu thành công.");
    }

    public async Task<ApiResponse<IEnumerable<UserResponse>>> GetAllUsersAsync()
    {
        var accounts = await _accountRepository.GetAllAsync();
        var users = accounts.Select(a => new UserResponse(
            a.Id,
            a.Email,
            a.FullName,
            a.AvatarUrl,
            a.Role.ToString(),
            a.IsLocked,
            a.CreatedAt,
            a.WarehouseId));
        return ApiResponse<IEnumerable<UserResponse>>.SuccessResponse(users);
    }

    public async Task<ApiResponse<PagedUserResponse>> GetPagedUsersAsync(GetUsersQuery request)
    {
        var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        Role? role = Enum.TryParse<Role>(request.Role, true, out var parsedRole)
            ? parsedRole
            : null;

        var (items, totalCount) = await _accountRepository.GetPagedAsync(
            pageNumber,
            pageSize,
            request.SearchTerm,
            request.Email,
            request.FullName,
            role,
            request.IsLocked,
            request.WarehouseId,
            request.CreatedFrom,
            request.CreatedTo
        );

        var users = items
            .Select(a => new UserResponse(
                a.Id,
                a.Email,
                a.FullName,
                a.AvatarUrl,
                a.Role.ToString(),
                a.IsLocked,
                a.CreatedAt,
                a.WarehouseId))
            .ToList();

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return ApiResponse<PagedUserResponse>.SuccessResponse(
            new PagedUserResponse(
                users,
                pageNumber,
                pageSize,
                totalCount,
                totalPages,
                pageNumber < totalPages,
                pageNumber > 1
            )
        );
    }

    public async Task<ApiResponse<UserStatsResponse>> GetUserStatsAsync()
    {
        var accounts = await _accountRepository.GetAllAsync();
        var accountList = accounts.ToList();

        var stats = new UserStatsResponse(
            accountList.Count,
            accountList.Count(account => !account.IsLocked),
            accountList.Count(account => account.IsLocked),
            accountList.Count(account => account.Role == Role.WarehouseStaff),
            accountList.Count(account => account.Role == Role.SaleStaff),
            accountList.Count(account => account.Role == Role.Manager)
        );

        return ApiResponse<UserStatsResponse>.SuccessResponse(stats);
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

    public async Task<ApiResponse> UpdateAvatarAsync(Guid accountId, string avatarUrl)
    {
        var account = await _accountRepository.GetByIdAsync(accountId);
        if (account == null)
            return ApiResponse.FailResponse("Account not found");

        account.UpdateProfile(account.FullName, avatarUrl);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse.SuccessResponse("Avatar updated successfully");
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
            roleStr, roleStr, account.WarehouseId);

        var refreshToken = await _refreshTokenService.CreateAsync(account.Id);

        return new TokenPairResponse(accessToken, refreshToken,
            new UserInfoResponse(account.Id, account.Email, roles, account.FullName, account.AvatarUrl, account.WarehouseId));
    }

    private static string GenerateTemporaryPassword()
    {
        const string uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lowercase = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%^&*";
        var allChars = uppercase + lowercase + digits + symbols;

        var passwordChars = new List<char>
        {
            uppercase[RandomNumberGenerator.GetInt32(uppercase.Length)],
            lowercase[RandomNumberGenerator.GetInt32(lowercase.Length)],
            digits[RandomNumberGenerator.GetInt32(digits.Length)],
            symbols[RandomNumberGenerator.GetInt32(symbols.Length)]
        };

        for (var i = passwordChars.Count; i < 12; i++)
        {
            passwordChars.Add(allChars[RandomNumberGenerator.GetInt32(allChars.Length)]);
        }

        for (var i = passwordChars.Count - 1; i > 0; i--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(i + 1);
            (passwordChars[i], passwordChars[swapIndex]) = (passwordChars[swapIndex], passwordChars[i]);
        }

        return new string(passwordChars.ToArray());
    }

    private static string BuildForgotPasswordEmailBody(string fullName, string temporaryPassword)
    {
        var displayName = string.IsNullOrWhiteSpace(fullName) ? "bạn" : fullName;

        return $"""
            <p>Xin chào {displayName},</p>
            <p>Hệ thống SensorX đã tạo một mật khẩu tạm thời mới cho tài khoản của bạn.</p>
            <p><strong>Mật khẩu mới:</strong> {temporaryPassword}</p>
            <p>Vì lý do bảo mật, vui lòng đăng nhập và đổi mật khẩu ngay sau khi nhận được email này.</p>
            <p>Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng liên hệ quản trị viên hoặc bộ phận hỗ trợ ngay lập tức.</p>
            <p>Trân trọng,<br/>SensorX</p>
            """;
    }
}
