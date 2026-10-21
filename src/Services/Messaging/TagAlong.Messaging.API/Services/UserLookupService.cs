using System.Text.Json;
using System.Text.Json.Serialization;

namespace TagAlong.Messaging.API.Services;

public interface IUserLookupService
{
    Task<string?> GetDisplayNameAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Whether the person has verified their identity; null if the user service couldn't say.</summary>
    Task<bool?> IsVerifiedAsync(Guid userId, CancellationToken cancellationToken = default);
}

public static class RideRules
{
    public const string VerifyBeforeRequesting =
        "Verify your identity before requesting a ride. It takes about a minute with your NIN and a selfie.";
}

public class UserLookupService : IUserLookupService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserLookupService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public UserLookupService(HttpClient httpClient, ILogger<UserLookupService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string?> GetDisplayNameAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/users/{userId}", cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var dto = JsonSerializer.Deserialize<UserPublicDto>(json, _jsonOptions);
            if (dto == null) return null;

            return $"{dto.FirstName} {dto.LastName}".Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to look up display name for user {UserId}", userId);
            return null;
        }
    }

    public async Task<bool?> IsVerifiedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/users/{userId}", cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return doc.RootElement.TryGetProperty("isVerified", out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? v.GetBoolean() : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check verification for user {UserId}", userId);
            return null;
        }
    }

    private record UserPublicDto(
        [property: JsonPropertyName("firstName")] string FirstName,
        [property: JsonPropertyName("lastName")] string LastName);
}
