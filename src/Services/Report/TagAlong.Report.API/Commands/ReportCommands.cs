using FluentValidation;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.EventBus;
using TagAlong.Report.API.DTOs;
using TagAlong.Report.Domain.Entities;
using TagAlong.Report.Domain.Repositories;

namespace TagAlong.Report.API.Commands;

// Create User Report
public record CreateUserReportCommand(
    Guid ReporterId,
    Guid ReportedUserId,
    string Reason,
    string Description) : ICommand<ReportDto>;

public class CreateUserReportCommandValidator : AbstractValidator<CreateUserReportCommand>
{
    public CreateUserReportCommandValidator()
    {
        RuleFor(x => x.ReporterId).NotEmpty();
        RuleFor(x => x.ReportedUserId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
    }
}

public class CreateUserReportCommandHandler : ICommandHandler<CreateUserReportCommand, ReportDto>
{
    private readonly IReportRepository _reportRepository;
    private readonly ILogger<CreateUserReportCommandHandler> _logger;

    public CreateUserReportCommandHandler(
        IReportRepository reportRepository,
        ILogger<CreateUserReportCommandHandler> logger)
    {
        _reportRepository = reportRepository;
        _logger = logger;
    }

    public async Task<Result<ReportDto>> Handle(CreateUserReportCommand request, CancellationToken cancellationToken)
    {
        var alreadyReported = await _reportRepository.HasUserReportedAsync(
            request.ReporterId, request.ReportedUserId, null, cancellationToken);

        if (alreadyReported)
            return Result.Failure<ReportDto>(Error.Conflict("You have already reported this user"));

        if (!Enum.TryParse<ReportReason>(request.Reason, true, out var reason))
            reason = ReportReason.Other;

        var report = Domain.Entities.Report.CreateUserReport(
            request.ReporterId,
            request.ReportedUserId,
            reason,
            request.Description);

        await _reportRepository.AddAsync(report, cancellationToken);
        await _reportRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User report {ReportId} created against user {ReportedUserId}",
            report.Id, request.ReportedUserId);

        return Result.Success(MapToDto(report));
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

// Create Delivery Report
public record CreateDeliveryReportCommand(
    Guid ReporterId,
    Guid ReportedDeliveryId,
    string Reason,
    string Description) : ICommand<ReportDto>;

public class CreateDeliveryReportCommandValidator : AbstractValidator<CreateDeliveryReportCommand>
{
    public CreateDeliveryReportCommandValidator()
    {
        RuleFor(x => x.ReporterId).NotEmpty();
        RuleFor(x => x.ReportedDeliveryId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
    }
}

public class CreateDeliveryReportCommandHandler : ICommandHandler<CreateDeliveryReportCommand, ReportDto>
{
    private readonly IReportRepository _reportRepository;
    private readonly ILogger<CreateDeliveryReportCommandHandler> _logger;

    public CreateDeliveryReportCommandHandler(
        IReportRepository reportRepository,
        ILogger<CreateDeliveryReportCommandHandler> logger)
    {
        _reportRepository = reportRepository;
        _logger = logger;
    }

    public async Task<Result<ReportDto>> Handle(CreateDeliveryReportCommand request, CancellationToken cancellationToken)
    {
        var alreadyReported = await _reportRepository.HasUserReportedAsync(
            request.ReporterId, null, request.ReportedDeliveryId, cancellationToken);

        if (alreadyReported)
            return Result.Failure<ReportDto>(Error.Conflict("You have already reported this delivery"));

        if (!Enum.TryParse<ReportReason>(request.Reason, true, out var reason))
            reason = ReportReason.Other;

        var report = Domain.Entities.Report.CreateDeliveryReport(
            request.ReporterId,
            request.ReportedDeliveryId,
            reason,
            request.Description);

        await _reportRepository.AddAsync(report, cancellationToken);
        await _reportRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Delivery report {ReportId} created against delivery {ReportedDeliveryId}",
            report.Id, request.ReportedDeliveryId);

        return Result.Success(MapToDto(report));
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

// Review Report
public record ReviewReportCommand(Guid ReportId, Guid ReviewerId, string AdminNotes) : ICommand<ReportDto?>;

public class ReviewReportCommandHandler : ICommandHandler<ReviewReportCommand, ReportDto?>
{
    private readonly IReportRepository _reportRepository;

    public ReviewReportCommandHandler(IReportRepository reportRepository)
    {
        _reportRepository = reportRepository;
    }

    public async Task<Result<ReportDto?>> Handle(ReviewReportCommand request, CancellationToken cancellationToken)
    {
        var report = await _reportRepository.GetByIdAsync(request.ReportId, cancellationToken);
        if (report == null) return Result.Success<ReportDto?>(null);

        report.MarkAsReviewed(request.ReviewerId, request.AdminNotes);
        _reportRepository.Update(report);
        await _reportRepository.SaveChangesAsync(cancellationToken);

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

// Resolve Report
public record ResolveReportCommand(Guid ReportId, string Resolution) : ICommand<ReportDto?>;

public class ResolveReportCommandHandler : ICommandHandler<ResolveReportCommand, ReportDto?>
{
    private readonly IReportRepository _reportRepository;

    public ResolveReportCommandHandler(IReportRepository reportRepository)
    {
        _reportRepository = reportRepository;
    }

    public async Task<Result<ReportDto?>> Handle(ResolveReportCommand request, CancellationToken cancellationToken)
    {
        var report = await _reportRepository.GetByIdAsync(request.ReportId, cancellationToken);
        if (report == null) return Result.Success<ReportDto?>(null);

        report.Resolve(request.Resolution);
        _reportRepository.Update(report);
        await _reportRepository.SaveChangesAsync(cancellationToken);

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

// Reject Report
public record RejectReportCommand(Guid ReportId, string Resolution) : ICommand<ReportDto?>;

public class RejectReportCommandHandler : ICommandHandler<RejectReportCommand, ReportDto?>
{
    private readonly IReportRepository _reportRepository;

    public RejectReportCommandHandler(IReportRepository reportRepository)
    {
        _reportRepository = reportRepository;
    }

    public async Task<Result<ReportDto?>> Handle(RejectReportCommand request, CancellationToken cancellationToken)
    {
        var report = await _reportRepository.GetByIdAsync(request.ReportId, cancellationToken);
        if (report == null) return Result.Success<ReportDto?>(null);

        report.Reject(request.Resolution);
        _reportRepository.Update(report);
        await _reportRepository.SaveChangesAsync(cancellationToken);

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
