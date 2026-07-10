using Microsoft.EntityFrameworkCore;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.User.API.DTOs;
using TagAlong.User.Infrastructure.Persistence;

namespace TagAlong.User.API.Queries;

public record SearchUsersQuery(string Q) : IQuery<IEnumerable<UserSearchResultDto>>;

public class SearchUsersQueryHandler : IQueryHandler<SearchUsersQuery, IEnumerable<UserSearchResultDto>>
{
    private readonly UserDbContext _db;

    public SearchUsersQueryHandler(UserDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IEnumerable<UserSearchResultDto>>> Handle(SearchUsersQuery request, CancellationToken cancellationToken)
    {
        var q = request.Q.Trim().ToLower();

        var results = await _db.UserProfiles
            .Where(u => u.Email.ToLower().Contains(q) || u.PhoneNumber.Contains(q))
            .Take(10)
            .Select(u => new UserSearchResultDto(
                u.AuthUserId.ToString(),
                u.FirstName + " " + u.LastName,
                u.Email))
            .ToListAsync(cancellationToken);

        return Result.Success<IEnumerable<UserSearchResultDto>>(results);
    }
}
