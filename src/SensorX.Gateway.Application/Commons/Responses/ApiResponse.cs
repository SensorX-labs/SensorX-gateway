namespace SensorX.Gateway.Application.Commons.Responses;

public class ApiResponse
{
    public bool Success { get; set; } = true;
    public dynamic? Data { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Token { get; set; }

    // Success có data
    public static ApiResponse SuccessResponse(dynamic data, string message = "") => new() { Success = true, Data = data, Message = message };

    // Success không có data
    public static ApiResponse SuccessResponse(string message) => new() { Success = true, Data = default, Message = message };

    // Fail
    public static ApiResponse FailResponse(string message) => new() { Success = false, Data = default, Message = message };
}

public class ApiResponse<T> : ApiResponse
{
    public new T? Data { get; set; }
    
    public static ApiResponse<T> SuccessResponse(T data, string message = "") => new() { Success = true, Data = data, Message = message };
    public new static ApiResponse<T> FailResponse(string message) => new() { Success = false, Data = default, Message = message };
}
