using Microsoft.EntityFrameworkCore;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Domain.Repositories;
using TagAlong.User.Infrastructure.Persistence;

namespace TagAlong.User.Infrastructure.Repositories;

public class NinCacheRepository : INinCacheRepository
{
    private readonly UserDbContext _context;

    private readonly byte[] _key;

    public NinCacheRepository(UserDbContext context, Microsoft.Extensions.Configuration.IConfiguration config)
    {
        var configured = config["Kyc:NinHashKey"];
        _key = !string.IsNullOrWhiteSpace(configured)
            ? System.Text.Encoding.UTF8.GetBytes(configured)
            : System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes("tagalong-nin:" + (config["JwtSettings:SecretKey"] ?? "")));
        _context = context;
    }

    public async Task<NinCache?> GetByNinAsync(string nin, CancellationToken cancellationToken = default)
    {
        var key = KeyFor(nin);
        return await _context.NinCaches.FirstOrDefaultAsync(n => n.NIN == key, cancellationToken);
    }

    public string KeyFor(string nin) =>
        Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(_key, System.Text.Encoding.UTF8.GetBytes(nin.Trim()))).ToLowerInvariant();

    public async Task AddAsync(NinCache entry, CancellationToken cancellationToken = default)
        => await _context.NinCaches.AddAsync(entry, cancellationToken);

    public void Update(NinCache entry)
        => _context.NinCaches.Update(entry);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}
