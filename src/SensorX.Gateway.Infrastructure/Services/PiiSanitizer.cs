using System.Text.Json;

namespace SensorX.Gateway.Infrastructure.Services;

public class PiiSanitizer
{
    private static readonly HashSet<string> SensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "newPassword", "oldPassword", "confirmPassword",
        "refreshToken", "accessToken", "token", "secret",
        "clientSecret", "totpCode", "code", "mfaToken"
    };

    public JsonDocument? Sanitize(string? userAgent, string? contentType, string? queryString)
    {
        try
        {
            var data = new Dictionary<string, object?>
            {
                ["userAgent"] = userAgent,
                ["contentType"] = contentType,
                ["queryString"] = queryString
            };
            return JsonSerializer.SerializeToDocument(data);
        }
        catch { return null; }
    }

    public static string MaskEmail(string email)
    {
        var parts = email.Split('@');
        if (parts.Length != 2) return "***";
        var local = parts[0];
        var masked = local.Length <= 2 ? "***" : $"{local[0]}***{local[^1]}";
        return $"{masked}@{parts[1]}";
    }

    public static bool IsSensitiveField(string fieldName) => SensitiveFields.Contains(fieldName);
}
