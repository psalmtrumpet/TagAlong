using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TagAlong.User.Infrastructure.Services;

public class FileService
{
    private readonly string _uploadsRoot;
    private readonly ILogger<FileService> _logger;

    public FileService(IConfiguration configuration, ILogger<FileService> logger)
    {
        _uploadsRoot = configuration["FileStorage:UploadsPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        _logger = logger;
        Directory.CreateDirectory(_uploadsRoot);
    }

    /// <summary>
    /// Saves a base64-encoded image string to disk. Returns the relative path.
    /// </summary>
    public async Task<string?> SaveBase64ImageAsync(string base64Data, string folder, string filename)
    {
        try
        {
            var cleaned = base64Data.Contains(',') ? base64Data.Split(',')[1] : base64Data;
            var bytes = Convert.FromBase64String(cleaned);
            var dir = Path.Combine(_uploadsRoot, folder);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{filename}.jpg");
            await File.WriteAllBytesAsync(path, bytes);
            return $"uploads/{folder}/{filename}.jpg";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save base64 image");
            return null;
        }
    }

    /// <summary>
    /// Downloads an image from a URL and saves to disk. Returns the relative path.
    /// </summary>
    public async Task<string?> SaveFromUrlAsync(HttpClient http, string imageUrl, string folder, string filename)
    {
        try
        {
            var bytes = await http.GetByteArrayAsync(imageUrl);
            var dir = Path.Combine(_uploadsRoot, folder);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{filename}.jpg");
            await File.WriteAllBytesAsync(path, bytes);
            return $"uploads/{folder}/{filename}.jpg";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download image from URL {Url}", imageUrl);
            return null;
        }
    }
}
