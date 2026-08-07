using Microsoft.EntityFrameworkCore;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Infrastructure.Persistence;

namespace TagAlong.User.API.Queries;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record AdminUserListItemDto(
    Guid Id,
    Guid AuthUserId,
    string Email,
    string FirstName,
    string LastName,
    string PhoneNumber,
    bool IsVerified,
    string VerificationStatus,
    bool IsSuspended,
    bool IsAvailable,
    decimal AverageRating,
    int CompletedDeliveries,
    int CompletedTrips,
    DateTime CreatedAt);

public record AdminUserDetailDto(
    Guid Id,
    Guid AuthUserId,
    string Email,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string? Bio,
    string? ProfileImageUrl,
    bool IsVerified,
    string VerificationStatus,
    DateTime? VerifiedAt,
    string? IdentityDocumentUrl,
    bool IsSuspended,
    DateTime? SuspendedAt,
    string? SuspensionReason,
    bool IsAvailable,
    decimal AverageRating,
    int TotalRatings,
    int CompletedDeliveries,
    int CompletedTrips,
    DateTime CreatedAt,
    // KYC fields
    string? KycNin,
    string? KycFirstName,
    string? KycLastName,
    string? KycMiddleName,
    string? KycDateOfBirth,
    string? KycGender,
    string? KycNationality,
    string? KycResidenceState,
    string? KycPhotoPath,
    string? KycStatus);

public record AdminUserListResult(IEnumerable<AdminUserListItemDto> Users, int TotalCount, int Page, int PageSize);

// ── Queries ───────────────────────────────────────────────────────────────────

public record AdminListUsersQuery(int Page, int PageSize, string Filter) : IQuery<AdminUserListResult>;

public class AdminListUsersQueryHandler : IQueryHandler<AdminListUsersQuery, AdminUserListResult>
{
    private readonly UserDbContext _db;

    public AdminListUsersQueryHandler(UserDbContext db) => _db = db;

    public async Task<Result<AdminUserListResult>> Handle(AdminListUsersQuery request, CancellationToken cancellationToken)
    {
        var query = _db.UserProfiles.IgnoreQueryFilters().AsQueryable();

        query = request.Filter switch
        {
            "verified"   => query.Where(u => u.IsVerified && !u.IsSuspended),
            "unverified" => query.Where(u => !u.IsVerified && !u.IsSuspended),
            "suspended"  => query.Where(u => u.IsSuspended),
            _            => query
        };

        var total = await query.CountAsync(cancellationToken);

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new AdminUserListItemDto(
                u.Id, u.AuthUserId, u.Email, u.FirstName, u.LastName, u.PhoneNumber,
                u.IsVerified, u.VerificationStatus.ToString(), u.IsSuspended,
                u.IsAvailable, u.AverageRating, u.CompletedDeliveries, u.CompletedTrips, u.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new AdminUserListResult(users, total, request.Page, request.PageSize));
    }
}

public record AdminGetUserDetailQuery(Guid AuthUserId) : IQuery<AdminUserDetailDto?>;

public class AdminGetUserDetailQueryHandler : IQueryHandler<AdminGetUserDetailQuery, AdminUserDetailDto?>
{
    private readonly UserDbContext _db;

    public AdminGetUserDetailQueryHandler(UserDbContext db) => _db = db;

    public async Task<Result<AdminUserDetailDto?>> Handle(AdminGetUserDetailQuery request, CancellationToken cancellationToken)
    {
        var profile = await _db.UserProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.AuthUserId == request.AuthUserId, cancellationToken);

        if (profile == null)
            return Result.Success<AdminUserDetailDto?>(null);

        var kyc = await _db.KycVerifications
            .Where(k => k.AuthUserId == request.AuthUserId)
            .OrderByDescending(k => k.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return Result.Success<AdminUserDetailDto?>(new AdminUserDetailDto(
            profile.Id, profile.AuthUserId, profile.Email, profile.FirstName, profile.LastName,
            profile.PhoneNumber, profile.Bio, profile.ProfileImageUrl,
            profile.IsVerified, profile.VerificationStatus.ToString(), profile.VerifiedAt,
            profile.IdentityDocumentUrl, profile.IsSuspended, profile.SuspendedAt, profile.SuspensionReason,
            profile.IsAvailable, profile.AverageRating, profile.TotalRatings,
            profile.CompletedDeliveries, profile.CompletedTrips, profile.CreatedAt,
            kyc?.NIN, kyc?.FirstName, kyc?.LastName, kyc?.MiddleName,
            kyc?.DateOfBirth, kyc?.Gender, kyc?.Nationality, kyc?.ResidenceState,
            kyc?.PhotoPath, kyc?.Status.ToString()));
    }
}

public record AdminGetStatsQuery : IQuery<AdminStatsDto>;

public record AdminStatsDto(
    int TotalUsers,
    int VerifiedUsers,
    int UnverifiedUsers,
    int SuspendedUsers,
    int ActiveUsers);

public class AdminGetStatsQueryHandler : IQueryHandler<AdminGetStatsQuery, AdminStatsDto>
{
    private readonly UserDbContext _db;

    public AdminGetStatsQueryHandler(UserDbContext db) => _db = db;

    public async Task<Result<AdminStatsDto>> Handle(AdminGetStatsQuery request, CancellationToken cancellationToken)
    {
        var total     = await _db.UserProfiles.IgnoreQueryFilters().CountAsync(cancellationToken);
        var verified  = await _db.UserProfiles.IgnoreQueryFilters().CountAsync(u => u.IsVerified && !u.IsSuspended, cancellationToken);
        var suspended = await _db.UserProfiles.IgnoreQueryFilters().CountAsync(u => u.IsSuspended, cancellationToken);
        var active    = await _db.UserProfiles.IgnoreQueryFilters().CountAsync(u => u.IsAvailable, cancellationToken);

        return Result.Success(new AdminStatsDto(total, verified, total - verified - suspended, suspended, active));
    }
}
