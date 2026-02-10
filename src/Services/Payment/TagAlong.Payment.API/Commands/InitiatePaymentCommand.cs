using FluentValidation;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.EventBus;
using TagAlong.Payment.API.DTOs;
using TagAlong.Payment.API.IntegrationEvents;
using TagAlong.Payment.Domain.Repositories;

namespace TagAlong.Payment.API.Commands;

public record InitiatePaymentCommand(
    Guid DeliveryId,
    Guid SenderId,
    Guid TravelerId,
    decimal Amount,
    decimal PlatformFeePercentage,
    string? PaymentMethod,
    string? PaymentProvider) : ICommand<PaymentDto>;

public class InitiatePaymentCommandValidator : AbstractValidator<InitiatePaymentCommand>
{
    public InitiatePaymentCommandValidator()
    {
        RuleFor(x => x.DeliveryId).NotEmpty();
        RuleFor(x => x.SenderId).NotEmpty();
        RuleFor(x => x.TravelerId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.PlatformFeePercentage).InclusiveBetween(0, 100);
    }
}

public class InitiatePaymentCommandHandler : ICommandHandler<InitiatePaymentCommand, PaymentDto>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IEventBus _eventBus;
    private readonly ILogger<InitiatePaymentCommandHandler> _logger;

    public InitiatePaymentCommandHandler(
        IPaymentRepository paymentRepository,
        IEventBus eventBus,
        ILogger<InitiatePaymentCommandHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<Result<PaymentDto>> Handle(InitiatePaymentCommand request, CancellationToken cancellationToken)
    {
        var existingPayment = await _paymentRepository.GetByDeliveryIdAsync(request.DeliveryId, cancellationToken);
        if (existingPayment != null)
        {
            return Result.Failure<PaymentDto>(Error.Conflict("Payment already exists for this delivery"));
        }

        var payment = Domain.Entities.Payment.Create(
            request.DeliveryId,
            request.SenderId,
            request.TravelerId,
            request.Amount,
            request.PlatformFeePercentage,
            request.PaymentMethod,
            request.PaymentProvider);

        await _paymentRepository.AddAsync(payment, cancellationToken);
        await _paymentRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Payment {PaymentId} initiated for delivery {DeliveryId}", payment.Id, request.DeliveryId);

        await _eventBus.PublishAsync(new PaymentInitiatedIntegrationEvent(
            payment.Id,
            payment.DeliveryId,
            payment.SenderId,
            payment.TravelerId,
            payment.Amount,
            payment.PlatformFee,
            payment.TravelerPayout));

        return Result.Success(new PaymentDto(
            payment.Id,
            payment.DeliveryId,
            payment.SenderId,
            payment.TravelerId,
            payment.Amount,
            payment.PlatformFee,
            payment.TravelerPayout,
            payment.Status.ToString(),
            payment.PaymentMethod,
            payment.TransactionReference,
            payment.PaymentProvider,
            payment.PaidAt,
            payment.CreatedAt));
    }
}
