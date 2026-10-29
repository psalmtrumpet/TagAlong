using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TagAlong.User.Domain.Entities;

namespace TagAlong.User.API.Services;

/// <summary>
/// Keeps <see cref="DriverGeoIndex"/> in step with the database: whenever a
/// profile is saved (went available, moved, went offline, expired), its position
/// is copied to Redis right after the save. One hook instead of one call at each
/// of the many places availability changes.
/// </summary>
public class DriverGeoInterceptor : SaveChangesInterceptor
{
    private record Change(Guid DriverId, bool Available, double? Lat, double? Lng, DateTime? SeenUtc);

    private readonly DriverGeoIndex _index;
    private readonly ConditionalWeakTable<DbContext, List<Change>> _pending = new();

    public DriverGeoInterceptor(DriverGeoIndex index) => _index = index;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Apply(eventData.Context).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        await Apply(eventData.Context);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void Capture(DbContext? context)
    {
        if (context == null) return;
        var changes = context.ChangeTracker.Entries<UserProfile>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified)
            .Select(e => new Change(e.Entity.AuthUserId, e.Entity.IsAvailable,
                e.Entity.CurrentLatitude, e.Entity.CurrentLongitude, e.Entity.LocationUpdatedAt))
            .ToList();
        _pending.AddOrUpdate(context, changes);
    }

    private async Task Apply(DbContext? context)
    {
        if (context == null || !_pending.TryGetValue(context, out var changes)) return;
        _pending.Remove(context);
        foreach (var c in changes)
        {
            if (c.Available && c.Lat is { } lat && c.Lng is { } lng)
                await _index.SetAsync(c.DriverId, lat, lng, c.SeenUtc ?? DateTime.UtcNow);
            else
                await _index.RemoveAsync(c.DriverId);
        }
    }
}
