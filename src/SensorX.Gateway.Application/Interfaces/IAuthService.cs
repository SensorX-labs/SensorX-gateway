using SensorX.Gateway.Application.Commons.Responses;
using SensorX.Gateway.Application.DTOs;

namespace SensorX.Gateway.Application.Interfaces;

public interface IAuthService
{
    Task<ApiResponse<TokenPairResponse>> LoginAsync(LoginRequest request);
    Task<ApiResponse<TokenPairResponse>> RefreshAsync(RefreshRequest request);
    Task<ApiResponse> LogoutAsync(string? userIdString, LogoutRequest request);
    Task<ApiResponse<object>> RegisterAsync(RegisterRequest request);
    Task<ApiResponse<object>> CreateAccountAsync(RegisterRequest request);
    ApiResponse<IntrospectResponse> Introspect(IntrospectRequest request);
    Task<ApiResponse> RevokeAsync(string? userIdString);
    Task<ApiResponse> ChangePasswordAsync(string? userIdString, ChangePasswordRequest request);
}
