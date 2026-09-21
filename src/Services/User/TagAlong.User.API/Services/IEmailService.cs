namespace TagAlong.User.API.Services;

public interface IEmailService
{
    Task SendAsync(string to, string toName, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
