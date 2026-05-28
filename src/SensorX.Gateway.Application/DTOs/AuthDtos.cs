namespace SensorX.Gateway.Application.DTOs;

// ── Auth Requests ──
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record LogoutRequest(string RefreshToken);
public record RegisterRequest(string Email, string Password);
public record MfaRequest(string MfaToken, string Code);
public record IntrospectRequest(string Token);
public record RevokeRequest();
public record ForgotPasswordRequest(string Email);
public record ChangePasswordRequest(string OldPassword, string NewPassword);

// ── Auth Responses ──
public record TokenPairResponse(string AccessToken, string RefreshToken, UserInfoResponse User);
public record UserInfoResponse(Guid Id, string Email, List<string> Roles, string? FullName = null, string? AvatarUrl = null, Guid? WarehouseId = null);
public record MfaChallengeResponse(bool MfaRequired, string MfaToken);
public record IntrospectResponse(bool Active, string? Sub = null, string? Scope = null, string? Exp = null);
public record UserResponse(Guid Id, string Email, string FullName, string? AvatarUrl, string Role, bool IsLocked, DateTimeOffset CreatedAt, Guid? WarehouseId = null);
public record GetUsersQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    string? Email = null,
    string? FullName = null,
    string? Role = null,
    bool? IsLocked = null,
    Guid? WarehouseId = null,
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null
);
public record PagedUserResponse(
    IReadOnlyList<UserResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage
);
public record UserStatsResponse(
    int TotalCount,
    int ActiveCount,
    int LockedCount,
    int WarehouseStaffCount,
    int SaleStaffCount,
    int ManagerCount
);
