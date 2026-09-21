using System.Net;
using System.Net.Mail;

namespace TagAlong.User.API.Services;

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string to, string toName, string subject, string htmlBody,
        CancellationToken cancellationToken = default)
    {
        var host = _config["Email__SmtpHost"];
        var portStr = _config["Email__SmtpPort"];
        var user = _config["Email__SmtpUser"];
        var pass = _config["Email__SmtpPass"];
        var from = _config["Email__FromAddress"];
        var fromName = _config["Email__FromName"] ?? "TagAlong";

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(from))
        {
            _logger.LogWarning("Email not configured — skipping send to {To}: {Subject}", to, subject);
            return;
        }

        int port = int.TryParse(portStr, out var p) ? p : 587;

        try
        {
            using var client = new SmtpClient(host, port)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(user, pass),
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            var mail = new MailMessage
            {
                From = new MailAddress(from, fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            mail.To.Add(new MailAddress(to, toName));

            await client.SendMailAsync(mail, cancellationToken);
            _logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}: {Subject}", to, subject);
        }
    }
}
