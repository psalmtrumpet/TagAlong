namespace TagAlong.Report.Domain.Repositories;

public interface IReportRepository
{
    Task<Entities.Report?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Entities.Report>> GetByReporterIdAsync(Guid reporterId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<Entities.Report>> GetByReportedUserIdAsync(Guid reportedUserId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<Entities.Report>> GetByReportedDeliveryIdAsync(Guid reportedDeliveryId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Entities.Report>> GetByStatusAsync(Entities.ReportStatus status, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<Entities.Report>> GetPendingReportsAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> GetReportCountByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> HasUserReportedAsync(Guid reporterId, Guid? reportedUserId, Guid? reportedDeliveryId, CancellationToken cancellationToken = default);
    Task AddAsync(Entities.Report report, CancellationToken cancellationToken = default);
    void Update(Entities.Report report);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
