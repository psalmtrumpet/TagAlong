using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TagAlong.Common.Results;
using TagAlong.Identity.API.Commands;
using TagAlong.Identity.API.DTOs;

namespace TagAlong.Identity.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var command = new RegisterCommand(
            request.Email,
            request.Password,
            request.FirstName,
            request.LastName,
            request.PhoneNumber);

        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return ToActionResult(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var command = new LoginCommand(request.Email, request.Password);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return ToActionResult(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost("google")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        var command = new GoogleLoginCommand(request.IdToken);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return ToActionResult(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var command = new RefreshTokenCommand(request.AccessToken, request.RefreshToken);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return ToActionResult(result.Error);
        }

        return Ok(result.Value);
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] SessionTokenRequest? body,
        CancellationToken cancellationToken)
    {
        var command = new LogoutCommand(body?.RefreshToken);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return ToActionResult(result.Error);
        }

        return Ok(new { message = "Logged out successfully" });
    }

    [Authorize]
    [HttpPut("profile")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateProfileCommand(request.FirstName, request.LastName, request.PhoneNumber, request.Gender);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return ToActionResult(result.Error);

        return Ok(result.Value);
    }

    /// <summary>Email the signed-in user a new code to confirm their address.</summary>
    [Authorize]
    [HttpPost("email/send-code")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SendEmailCode(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SendEmailCodeCommand(), cancellationToken);
        if (result.IsFailure)
            return ToActionResult(result.Error);
        return Ok(new { message = "Code sent" });
    }

    /// <summary>Confirm the email address with the code; returns fresh tokens.</summary>
    [Authorize]
    [HttpPost("email/verify")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new VerifyEmailCommand(request.Code), cancellationToken);
        if (result.IsFailure)
            return ToActionResult(result.Error);
        return Ok(result.Value);
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var command = new ForgotPasswordCommand(request.Email);
        await _mediator.Send(command, cancellationToken);
        return Ok(new { message = "If an account exists, a reset code has been sent" });
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var command = new ResetPasswordCommand(request.Email, request.Code, request.NewPassword);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return ToActionResult(result.Error);

        return Ok(new { message = "Password reset successfully" });
    }

    /// <summary>How many phones are signed in to this account.</summary>
    [Authorize]
    [HttpGet("sessions")]
    public async Task<IActionResult> Sessions(
        [FromServices] TagAlong.Identity.API.Services.RefreshSessions sessions, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId))
            return Unauthorized();
        return Ok(new { active = await sessions.CountActiveAsync(userId, cancellationToken) });
    }

    /// <summary>Sign out every other phone; this one (the token sent) stays signed in.</summary>
    [Authorize]
    [HttpPost("sessions/sign-out-others")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> SignOutOthers(
        [FromBody] SessionTokenRequest body,
        [FromServices] TagAlong.Identity.API.Services.RefreshSessions sessions,
        [FromServices] TagAlong.Identity.Domain.Repositories.IUserRepository users,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId))
            return Unauthorized();
        if (string.IsNullOrWhiteSpace(body.RefreshToken)) return BadRequest(new { error = "Missing refresh token" });
        var n = await sessions.RevokeAllAsync(userId, "signed out by another phone", cancellationToken, keepToken: body.RefreshToken);
        // The old shared sign-in (from before per-phone sessions) ends too
        if (await users.GetByIdAsync(userId, cancellationToken) is { RefreshToken: not null } user)
        {
            user.RevokeRefreshToken();
            users.Update(user);
            await users.SaveChangesAsync(cancellationToken);
        }
        return Ok(new { signedOut = n });
    }

    private IActionResult ToActionResult(Error error)
    {
        return error.Type switch
        {
            ErrorType.NotFound => NotFound(new { error = error.Message }),
            ErrorType.Unauthorized => Unauthorized(new { error = error.Message }),
            ErrorType.Conflict => Conflict(new { error = error.Message }),
            ErrorType.Validation => BadRequest(new { error = error.Message }),
            _ => BadRequest(new { error = error.Message })
        };
    }
}

/// <summary>This phone's refresh token (to sign out just this phone, or keep it signed in).</summary>
public record SessionTokenRequest(string? RefreshToken);
