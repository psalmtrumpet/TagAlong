using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.Identity.Domain.Repositories;

namespace TagAlong.Identity.API.Commands;

/// <param name="RefreshToken">This phone's token: only its session ends. Without it, every session ends.</param>
public record LogoutCommand(string? RefreshToken = null) : ICommand;

public class LogoutCommandHandler : ICommandHandler<LogoutCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;

    private readonly TagAlong.Identity.API.Services.RefreshSessions _sessions;

    public LogoutCommandHandler(IUserRepository userRepository, IHttpContextAccessor httpContextAccessor,
        TagAlong.Identity.API.Services.RefreshSessions sessions)
    {
        _sessions = sessions;
        _userRepository = userRepository;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Result.Failure(Error.Unauthorized("Invalid user"));
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

        if (user == null)
        {
            return Result.Failure(Error.NotFound($"User with id {userId} not found"));
        }

        if (request.RefreshToken is { Length: > 0 } token)
        {
            await _sessions.RevokeAsync(userId, token, "signed out", cancellationToken);
            return Result.Success();
        }
        user.RevokeRefreshToken();
        await _sessions.RevokeAllAsync(userId, "signed out", cancellationToken);

        _userRepository.Update(user);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
