using FluentValidation;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.EventBus;
using TagAlong.Payment.API.DTOs;
using TagAlong.Payment.API.IntegrationEvents;
using TagAlong.Payment.Domain.Repositories;

namespace TagAlong.Payment.API.Commands;

public record RefundPaymentCommand(
    Guid PaymentId,
    string? Reason) : ICommand<PaymentDto>;

public class RefundPaymentCommandValidator : AbstractValidator<RefundPaymentCommand>
{
    public RefundPaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500).When(x => x.Reason != null);
    }
}

public class RefundPaymentCommandHandler : ICommandHandler<RefundPaymentCommand, PaymentDto>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IEventBus _eventBus;
    private readonly ILogger<RefundPaymentCommandHandler> _logger;

    public RefundPaymentCommandHandler(
        IPaymentRepository paymentRepository,
        IEventBus eventBus,
        ILogger<RefundPaymentCommandHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<Result<PaymentDto>> Handle(RefundPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.GetByIdAsync(request.PaymentId, cancellationToken);
        if (payment == null)
        {
            return Result.Failure<PaymentDto>(Error.NotFound("Payment not found"));
        }

        payment.MarkAsRefunded();
        _paymentRepository.Update(payment);
        await _paymentRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Payment {PaymentId} refunded. Reason: {Reason}", payment.Id, request.Reason);

        await _eventBus.PublishAsync(new PaymentRefundedIntegrationEvent(
            payment.Id,
            payment.DeliveryId,
            payment.SenderId,
            payment.TravelerId,
            payment.Amount,
            request.Reason));

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
