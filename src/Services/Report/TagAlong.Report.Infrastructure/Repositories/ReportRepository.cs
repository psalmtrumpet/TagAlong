using Microsoft.EntityFrameworkCore;
using TagAlong.Report.Domain.Entities;
using TagAlong.Report.Domain.Repositories;
using TagAlong.Report.Infrastructure.Persistence;

namespace TagAlong.Report.Infrastructure.Repositories;

public class ReportRepository : IReportRepository
{
    private readonly ReportDbContext _context;

    public ReportRepository(ReportDbContext context)
    {
        _context = context;
    }

    public async Task<Domain.Entities.Report?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Reports.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Domain.Entities.Report>> GetByReporterIdAsync(Guid reporterId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return await _context.Reports
            .Where(r => r.ReporterId == reporterId)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Domain.Entities.Report>> GetByReportedUserIdAsync(Guid reportedUserId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return await _context.Reports
            .Where(r => r.ReportedUserId == reportedUserId)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Domain.Entities.Report>> GetByReportedDeliveryIdAsync(Guid reportedDeliveryId, CancellationToken cancellationToken = default)
    {
        return await _context.Reports
            .Where(r => r.ReportedDeliveryId == reportedDeliveryId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Domain.Entities.Report>> GetByStatusAsync(ReportStatus status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return await _context.Reports
            .Where(r => r.Status == status)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Domain.Entities.Report>> GetPendingReportsAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return await _context.Reports
            .Where(r => r.Status == ReportStatus.Pending || r.Status == ReportStatus.UnderReview)
            .OrderBy(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetReportCountByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Reports
            .Where(r => r.ReportedUserId == userId)
            .CountAsync(cancellationToken);
    }

    public async Task<bool> HasUserReportedAsync(Guid reporterId, Guid? reportedUserId, Guid? reportedDeliveryId, CancellationToken cancellationToken = default)
    {
        return await _context.Reports
            .AnyAsync(r => r.ReporterId == reporterId &&
                          (reportedUserId.HasValue && r.ReportedUserId == reportedUserId ||
                           reportedDeliveryId.HasValue && r.ReportedDeliveryId == reportedDeliveryId),
                      cancellationToken);
    }

    public async Task AddAsync(Domain.Entities.Report report, CancellationToken cancellationToken = default)
    {
        await _context.Reports.AddAsync(report, cancellationToken);
    }

    public void Update(Domain.Entities.Report report)
    {
        _context.Reports.Update(report);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
