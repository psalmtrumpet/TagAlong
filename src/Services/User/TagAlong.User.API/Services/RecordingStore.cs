using System.Security.Cryptography;
using System.Text;

namespace TagAlong.User.API.Services;

/// <summary>
/// Saves ride safety recordings encrypted (AES-GCM) under the uploads folder,
/// and decrypts them for admins. The key is <c>Safety:RecordingKey</c>
/// (base64, 32 bytes) or, if unset, derived from the JWT secret.
/// File layout: 12-byte nonce, 16-byte tag, ciphertext.
/// </summary>
public class RecordingStore
{
    private readonly string _root;
    private readonly byte[] _key;

    public RecordingStore(IConfiguration config)
    {
        _root = Path.Combine(
            config["FileStorage:UploadsPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads"),
            "recordings");
        Directory.CreateDirectory(_root);

        var configured = config["Safety:RecordingKey"];
        _key = !string.IsNullOrWhiteSpace(configured)
            ? Convert.FromBase64String(configured)
            : SHA256.HashData(Encoding.UTF8.GetBytes("tagalong-recordings:" + (config["JwtSettings:SecretKey"] ?? "")));
        if (_key.Length != 32) throw new InvalidOperationException("Safety:RecordingKey must be 32 bytes (base64).");
    }

    /// <summary>Encrypts and saves; returns the path relative to the recordings folder.</summary>
    public async Task<string> SaveAsync(Guid userId, Guid recordingId, Stream audio, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await audio.CopyToAsync(buffer, ct);
        var plain = buffer.ToArray();

        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var cipher = new byte[plain.Length];
        using (var aes = new AesGcm(_key, 16)) aes.Encrypt(nonce, plain, cipher, tag);

        var dir = Path.Combine(_root, userId.ToString("N"));
        Directory.CreateDirectory(dir);
        var rel = $"{userId:N}/{recordingId:N}.bin";
        await using var file = File.Create(Path.Combine(_root, rel));
        await file.WriteAsync(nonce, ct);
        await file.WriteAsync(tag, ct);
        await file.WriteAsync(cipher, ct);
        return rel;
    }

    /// <summary>The decrypted audio, or null if the file is missing or damaged.</summary>
    public async Task<byte[]?> ReadAsync(string relativePath, CancellationToken ct)
    {
        var full = Resolve(relativePath);
        if (full == null) return null;
        var data = await File.ReadAllBytesAsync(full, ct);
        if (data.Length < 28) return null;
        var plain = new byte[data.Length - 28];
        try
        {
            using var aes = new AesGcm(_key, 16);
            aes.Decrypt(data.AsSpan(0, 12), data.AsSpan(28), data.AsSpan(12, 16), plain);
            return plain;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public void Delete(string relativePath)
    {
        var full = Resolve(relativePath);
        if (full != null) File.Delete(full);
    }

    private string? Resolve(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relativePath));
        var root = Path.GetFullPath(_root) + Path.DirectorySeparatorChar;
        return full.StartsWith(root, StringComparison.Ordinal) && File.Exists(full) ? full : null;
    }
}

/// <summary>Deletes safety recordings after 30 days (180 if attached to a report).</summary>
public class RecordingCleanupJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<RecordingCleanupJob> _logger;

    public RecordingCleanupJob(IServiceScopeFactory scopes, ILogger<RecordingCleanupJob> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromHours(6), stoppingToken); } catch (OperationCanceledException) { return; }
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TagAlong.User.Infrastructure.Persistence.UserDbContext>();
                var store = scope.ServiceProvider.GetRequiredService<RecordingStore>();
                var now = DateTime.UtcNow;
                var old = db.SafetyRecordings
                    .Where(r => r.Flagged ? r.CreatedAt < now.AddDays(-180) : r.CreatedAt < now.AddDays(-30))
                    .ToList();
                foreach (var r in old) store.Delete(r.FilePath);
                db.SafetyRecordings.RemoveRange(old);
                await db.SaveChangesAsync(stoppingToken);
                if (old.Count > 0) _logger.LogInformation("Deleted {Count} old safety recordings", old.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Safety recording cleanup failed");
            }
        }
    }
}
