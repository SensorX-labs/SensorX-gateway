namespace SensorX.Gateway.Application.DTOs;

// ── Auth Requests ──
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record LogoutRequest(string RefreshToken);
public record RegisterRequest(string Email, string Password);
public record MfaRequest(string MfaToken, string Code);
public record IntrospectRequest(string Token);
public record RevokeRequest();
public record ChangePasswordRequest(string OldPassword, string NewPassword);

// ── Auth Responses ──
public record TokenPairResponse(string AccessToken, string RefreshToken, UserInfoResponse User);
public record UserInfoResponse(Guid Id, string Email, List<string> Roles);
public record MfaChallengeResponse(bool MfaRequired, string MfaToken);
public record IntrospectResponse(bool Active, string? Sub = null, string? Scope = null, string? Exp = null);
public record UserResponse(Guid Id, string Email, string FullName, string Role, bool IsLocked, DateTimeOffset CreatedAt);
