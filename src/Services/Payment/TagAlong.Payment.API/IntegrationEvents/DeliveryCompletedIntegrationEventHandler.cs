using TagAlong.EventBus;
using TagAlong.Payment.Domain.Repositories;

namespace TagAlong.Payment.API.IntegrationEvents;

public record DeliveryCompletedIntegrationEvent(
    Guid DeliveryId,
    Guid PackageRequestId,
    Guid TripId,
    Guid SenderId,
    Guid TravelerId,
    decimal AgreedPrice,
    DateTime CompletedAt) : IntegrationEvent;

public class DeliveryCompletedIntegrationEventHandler : IIntegrationEventHandler<DeliveryCompletedIntegrationEvent>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IEventBus _eventBus;
    private readonly ILogger<DeliveryCompletedIntegrationEventHandler> _logger;
    private readonly IConfiguration _configuration;

    public DeliveryCompletedIntegrationEventHandler(
        IPaymentRepository paymentRepository,
        IEventBus eventBus,
        ILogger<DeliveryCompletedIntegrationEventHandler> logger,
        IConfiguration configuration)
    {
        _paymentRepository = paymentRepository;
        _eventBus = eventBus;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task HandleAsync(DeliveryCompletedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling DeliveryCompletedIntegrationEvent for delivery {DeliveryId}", @event.DeliveryId);

        var existingPayment = await _paymentRepository.GetByDeliveryIdAsync(@event.DeliveryId, cancellationToken);
        if (existingPayment != null)
        {
            _logger.LogWarning("Payment already exists for delivery {DeliveryId}", @event.DeliveryId);
            return;
        }

        var platformFeePercentage = _configuration.GetValue<decimal>("PlatformFeePercentage", 10m);

        var payment = Domain.Entities.Payment.Create(
            @event.DeliveryId,
            @event.SenderId,
            @event.TravelerId,
            @event.AgreedPrice,
            platformFeePercentage);

        await _paymentRepository.AddAsync(payment, cancellationToken);
        await _paymentRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Payment {PaymentId} created for delivery {DeliveryId}", payment.Id, @event.DeliveryId);

        await _eventBus.PublishAsync(new PaymentInitiatedIntegrationEvent(
            payment.Id,
            payment.DeliveryId,
            payment.SenderId,
            payment.TravelerId,
            payment.Amount,
            payment.PlatformFee,
            payment.TravelerPayout));
    }
}
