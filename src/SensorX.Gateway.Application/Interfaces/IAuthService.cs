using SensorX.Gateway.Application.Commons.Responses;
using SensorX.Gateway.Application.DTOs;

namespace SensorX.Gateway.Application.Interfaces;

public interface IAuthService
{
    Task<ApiResponse<TokenPairResponse>> LoginAsync(LoginRequest request);
    Task<ApiResponse<TokenPairResponse>> RefreshAsync(RefreshRequest request);
    Task<ApiResponse> LogoutAsync(string? userIdString, LogoutRequest request);
    ApiResponse<IntrospectResponse> Introspect(IntrospectRequest request);
    Task<ApiResponse> RevokeAsync(string? userIdString);
    Task<ApiResponse> ChangePasswordAsync(string? userIdString, ChangePasswordRequest request);
    Task<ApiResponse<IEnumerable<UserResponse>>> GetAllUsersAsync();
    Task<ApiResponse> ToggleUserLockAsync(Guid userId);
}
