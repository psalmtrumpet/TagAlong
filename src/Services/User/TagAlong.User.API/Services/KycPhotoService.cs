using Microsoft.EntityFrameworkCore;
using TagAlong.User.Infrastructure.Persistence;
using TagAlong.User.Infrastructure.Services;

namespace TagAlong.User.API.Services;

/// <summary>
/// Saves the selfie a user took during SmileID verification and attaches it to
/// their profile (kept private — served only to signed-in users).
/// </summary>
public class KycPhotoService
{
    private static readonly HttpClient Download = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger<KycPhotoService> _logger;

    public KycPhotoService(IServiceScopeFactory scopes, IConfiguration config, ILogger<KycPhotoService> logger)
    {
        _scopes = scopes;
        _config = config;
        _logger = logger;
    }

    /// <summary>Fire-and-forget after a successful verification.</summary>
    public void CaptureInBackground(Guid authUserId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                // SmileID can take a moment to make the images available
                await Task.Delay(TimeSpan.FromSeconds(5));
                await CaptureAsync(authUserId, CancellationToken.None);
            }
            catch (Exception ex) { _logger.LogError(ex, "Selfie capture failed for {UserId}", authUserId); }
        });
    }

    /// <returns>true if a photo is now attached.</returns>
    public async Task<bool> CaptureAsync(Guid authUserId, CancellationToken ct)
    {
        var apiKey = _config["SmileId:ApiKey"];
        var partnerId = _config["SmileId:PartnerId"];
        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(partnerId)) return false;

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
        var files = scope.ServiceProvider.GetRequiredService<FileService>();
        var smile = scope.ServiceProvider.GetRequiredService<SmileIdPollService>();

        var kyc = await db.KycVerifications
            .Where(k => k.AuthUserId == authUserId && k.SmileJobId != null && k.SmileUserId != null)
            .OrderByDescending(k => k.CompletedAt)
            .FirstOrDefaultAsync(ct);
        if (kyc == null) return false;

        // Already saved (and still on disk)
        if (files.ResolvePath(kyc.PhotoPath) != null) return true;

        var url = await smile.GetSelfieImageUrlAsync(kyc.SmileJobId!, kyc.SmileUserId!, apiKey, partnerId, ct);
        if (url == null) return false;

        var path = await files.SaveFromUrlAsync(Download, url, "kyc", authUserId.ToString());
        if (path == null) return false;

        kyc.AttachPhoto(path);
        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.AuthUserId == authUserId, ct);
        profile?.AttachVerificationPhoto(path);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Saved verification selfie for {UserId}", authUserId);
        return true;
    }
}
