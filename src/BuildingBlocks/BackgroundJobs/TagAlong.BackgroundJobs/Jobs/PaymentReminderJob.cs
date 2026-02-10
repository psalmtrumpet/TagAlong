using Microsoft.Extensions.Logging;

namespace TagAlong.BackgroundJobs.Jobs;

public interface IPaymentReminderService
{
    Task SendPaymentRemindersAsync(CancellationToken cancellationToken);
}

public class PaymentReminderJob : IRecurringJob
{
    private readonly IPaymentReminderService _paymentReminderService;
    private readonly ILogger<PaymentReminderJob> _logger;

    public PaymentReminderJob(
        IPaymentReminderService paymentReminderService,
        ILogger<PaymentReminderJob> logger)
    {
        _paymentReminderService = paymentReminderService;
        _logger = logger;
    }

    public string JobId => "payment-reminder-job";
    public string CronExpression => "0 9 * * *"; // Every day at 9 AM

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting payment reminder job");
        await _paymentReminderService.SendPaymentRemindersAsync(cancellationToken);
        _logger.LogInformation("Payment reminder job completed");
    }
}
