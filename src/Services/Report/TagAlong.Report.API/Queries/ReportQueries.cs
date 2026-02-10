using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.Report.API.DTOs;
using TagAlong.Report.Domain.Repositories;

namespace TagAlong.Report.API.Queries;

public record GetReportByIdQuery(Guid Id) : IQuery<ReportDto?>;
public record GetReportsByReporterQuery(Guid ReporterId, int Page, int PageSize) : IQuery<IEnumerable<ReportDto>>;
public record GetReportsByReportedUserQuery(Guid ReportedUserId, int Page, int PageSize) : IQuery<IEnumerable<ReportDto>>;
public record GetReportsByDeliveryQuery(Guid DeliveryId) : IQuery<IEnumerable<ReportDto>>;
public record GetPendingReportsQuery(int Page, int PageSize) : IQuery<IEnumerable<ReportDto>>;

public class GetReportByIdQueryHandler : IQueryHandler<GetReportByIdQuery, ReportDto?>
{
    private readonly IReportRepository _reportRepository;

    public GetReportByIdQueryHandler(IReportRepository reportRepository)
    {
        _reportRepository = reportRepository;
    }

    public async Task<Result<ReportDto?>> Handle(GetReportByIdQuery request, CancellationToken cancellationToken)
    {
        var report = await _reportRepository.GetByIdAsync(request.Id, cancellationToken);
        if (report == null) return Result.Success<ReportDto?>(null);

        return Result.Success<ReportDto?>(MapToDto(report));
    }

    private static ReportDto MapToDto(Domain.Entities.Report report) => new(
        report.Id,
        report.ReporterId,
        report.ReportedUserId,
        report.ReportedDeliveryId,
        report.ReportType.ToString(),
        report.Reason.ToString(),
        report.Description,
        report.Status.ToString(),
        report.AdminNotes,
        report.ReviewedBy,
        report.ReviewedAt,
        report.Resolution,
        report.CreatedAt);
}

public class GetReportsByReporterQueryHandler : IQueryHandler<GetReportsByReporterQuery, IEnumerable<ReportDto>>
{
    private readonly IReportRepository _reportRepository;

    public GetReportsByReporterQueryHandler(IReportRepository reportRepository)
    {
        _reportRepository = reportRepository;
    }

    public async Task<Result<IEnumerable<ReportDto>>> Handle(GetReportsByReporterQuery request, CancellationToken cancellationToken)
    {
        var reports = await _reportRepository.GetByReporterIdAsync(request.ReporterId, request.Page, request.PageSize, cancellationToken);
        return Result.Success(reports.Select(MapToDto));
    }

    private static ReportDto MapToDto(Domain.Entities.Report report) => new(
        report.Id,
        report.ReporterId,
        report.ReportedUserId,
        report.ReportedDeliveryId,
        report.ReportType.ToString(),
        report.Reason.ToString(),
        report.Description,
        report.Status.ToString(),
        report.AdminNotes,
        report.ReviewedBy,
        report.ReviewedAt,
        report.Resolution,
        report.CreatedAt);
}

public class GetReportsByReportedUserQueryHandler : IQueryHandler<GetReportsByReportedUserQuery, IEnumerable<ReportDto>>
{
    private readonly IReportRepository _reportRepository;

    public GetReportsByReportedUserQueryHandler(IReportRepository reportRepository)
    {
        _reportRepository = reportRepository;
    }

    public async Task<Result<IEnumerable<ReportDto>>> Handle(GetReportsByReportedUserQuery request, CancellationToken cancellationToken)
    {
        var reports = await _reportRepository.GetByReportedUserIdAsync(request.ReportedUserId, request.Page, request.PageSize, cancellationToken);
        return Result.Success(reports.Select(MapToDto));
    }

    private static ReportDto MapToDto(Domain.Entities.Report report) => new(
        report.Id,
        report.ReporterId,
        report.ReportedUserId,
        report.ReportedDeliveryId,
        report.ReportType.ToString(),
        report.Reason.ToString(),
        report.Description,
        report.Status.ToString(),
        report.AdminNotes,
        report.ReviewedBy,
        report.ReviewedAt,
        report.Resolution,
        report.CreatedAt);
}

public class GetReportsByDeliveryQueryHandler : IQueryHandler<GetReportsByDeliveryQuery, IEnumerable<ReportDto>>
{
    private readonly IReportRepository _reportRepository;

    public GetReportsByDeliveryQueryHandler(IReportRepository reportRepository)
    {
        _reportRepository = reportRepository;
    }

    public async Task<Result<IEnumerable<ReportDto>>> Handle(GetReportsByDeliveryQuery request, CancellationToken cancellationToken)
    {
        var reports = await _reportRepository.GetByReportedDeliveryIdAsync(request.DeliveryId, cancellationToken);
        return Result.Success(reports.Select(MapToDto));
    }

    private static ReportDto MapToDto(Domain.Entities.Report report) => new(
        report.Id,
        report.ReporterId,
        report.ReportedUserId,
        report.ReportedDeliveryId,
        report.ReportType.ToString(),
        report.Reason.ToString(),
        report.Description,
        report.Status.ToString(),
        report.AdminNotes,
        report.ReviewedBy,
        report.ReviewedAt,
        report.Resolution,
        report.CreatedAt);
}

public class GetPendingReportsQueryHandler : IQueryHandler<GetPendingReportsQuery, IEnumerable<ReportDto>>
{
    private readonly IReportRepository _reportRepository;

    public GetPendingReportsQueryHandler(IReportRepository reportRepository)
    {
        _reportRepository = reportRepository;
    }

    public async Task<Result<IEnumerable<ReportDto>>> Handle(GetPendingReportsQuery request, CancellationToken cancellationToken)
    {
        var reports = await _reportRepository.GetPendingReportsAsync(request.Page, request.PageSize, cancellationToken);
        return Result.Success(reports.Select(MapToDto));
    }

    private static ReportDto MapToDto(Domain.Entities.Report report) => new(
        report.Id,
        report.ReporterId,
        report.ReportedUserId,
        report.ReportedDeliveryId,
        report.ReportType.ToString(),
        report.Reason.ToString(),
        report.Description,
        report.Status.ToString(),
        report.AdminNotes,
        report.ReviewedBy,
        report.ReviewedAt,
        report.Resolution,
        report.CreatedAt);
}
