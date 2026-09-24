using System.Net;
using System.Net.Mail;

namespace TagAlong.Identity.Infrastructure.Services;

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _config;

    public SmtpEmailService(IConfiguration config) => _config = config;

    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        var host = _config["Email__SmtpHost"] ?? _config["Email:SmtpHost"] ?? "smtp.hostinger.com";
        var port = int.Parse(_config["Email__SmtpPort"] ?? _config["Email:SmtpPort"] ?? "465");
        var user = _config["Email__SmtpUser"] ?? _config["Email:SmtpUser"] ?? string.Empty;
        var pass = _config["Email__SmtpPass"] ?? _config["Email:SmtpPass"] ?? string.Empty;
        var from = _config["Email__FromAddress"] ?? _config["Email:FromAddress"] ?? user;
        var fromName = _config["Email__FromName"] ?? _config["Email:FromName"] ?? "TagAlong";

        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass)) return;

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(user, pass),
        };

        var message = new MailMessage
        {
            From = new MailAddress(from, fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
        };
        message.To.Add(new MailAddress(toEmail, toName));

        await client.SendMailAsync(message);
    }
}
