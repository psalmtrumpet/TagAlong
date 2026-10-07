using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TagAlong.User.API.Services;

public class SmileIdPollService
{
    private static readonly HttpClient _http = new();
    private readonly ILogger<SmileIdPollService> _logger;

    public SmileIdPollService(ILogger<SmileIdPollService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Calls SmileID get_job_status and returns the raw JSON of the inner "result" object
    /// if the job is complete, or null if still pending / on error.
    /// </summary>
    public async Task<string?> GetJobStatusResultJsonAsync(
        string jobId, string userId, string apiKey, string partnerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            var signature = ComputeSignature(timestamp, partnerId, apiKey);

            var body = new
            {
                partner_id = partnerId,
                timestamp,
                signature,
                user_id = userId,
                job_id = jobId,
                image_links = false,
                history = false
            };

            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(
                "https://api.smileidentity.com/v1/job_status", content, cancellationToken);

            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("SmileID job_status for job={JobId}: HTTP {Status}", jobId, (int)response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("SmileID job_status non-success for job={JobId}: {Body}", jobId, raw.Length > 500 ? raw[..500] : raw);
                return null;
            }

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (!root.TryGetProperty("job_complete", out var jobComplete) || !jobComplete.GetBoolean())
            {
                _logger.LogInformation("SmileID job_status: job={JobId} not yet complete", jobId);
                return null;
            }

            if (!root.TryGetProperty("result", out var result))
            {
                _logger.LogWarning("SmileID job_status: job={JobId} complete but no result object", jobId);
                return null;
            }

            // Return the result object as JSON so the webhook handler can process it
            var resultJson = result.GetRawText();
            _logger.LogInformation("SmileID job_status: job={JobId} complete, resultLen={L}", jobId, resultJson.Length);
            return resultJson;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SmileID job_status poll failed for job={JobId}", jobId);
            return null;
        }
    }

    /// <summary>
    /// Asks SmileID for a completed job's image links and returns the URL of the
    /// selfie the user took during verification, or null if unavailable.
    /// </summary>
    public async Task<string?> GetSelfieImageUrlAsync(
        string jobId, string userId, string apiKey, string partnerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            var body = new
            {
                partner_id = partnerId,
                timestamp,
                signature = ComputeSignature(timestamp, partnerId, apiKey),
                user_id = userId,
                job_id = jobId,
                image_links = true,
                history = false
            };
            using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(
                "https://api.smileidentity.com/v1/job_status", content, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("SmileID image links failed for job={JobId}: HTTP {Status}", jobId, (int)response.StatusCode);
                return null;
            }

            using var doc = JsonDocument.Parse(raw);
            // job_status returns "image_links" (lower-case) with selfie_image + id_photo_image
            if (doc.RootElement.TryGetProperty("image_links", out var links) &&
                links.ValueKind == JsonValueKind.Object &&
                links.TryGetProperty("selfie_image", out var selfie) &&
                selfie.ValueKind == JsonValueKind.String &&
                Uri.TryCreate(selfie.GetString(), UriKind.Absolute, out _))
            {
                return selfie.GetString();
            }
            _logger.LogInformation("SmileID job={JobId}: no selfie_image link in response", jobId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SmileID image links request failed for job={JobId}", jobId);
            return null;
        }
    }

    private static string ComputeSignature(string timestamp, string partnerId, string apiKey)
    {
        var message = $"{timestamp}{partnerId}sid_request";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return Convert.ToBase64String(hash);
    }
}
