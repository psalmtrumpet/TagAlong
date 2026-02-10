using FluentValidation;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.EventBus;
using TagAlong.Payment.API.DTOs;
using TagAlong.Payment.API.IntegrationEvents;
using TagAlong.Payment.Domain.Repositories;

namespace TagAlong.Payment.API.Commands;

public record ConfirmPaymentCommand(
    Guid PaymentId,
    string TransactionReference) : ICommand<PaymentDto>;

public class ConfirmPaymentCommandValidator : AbstractValidator<ConfirmPaymentCommand>
{
    public ConfirmPaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.TransactionReference).NotEmpty().MaximumLength(255);
    }
}

public class ConfirmPaymentCommandHandler : ICommandHandler<ConfirmPaymentCommand, PaymentDto>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ConfirmPaymentCommandHandler> _logger;

    public ConfirmPaymentCommandHandler(
        IPaymentRepository paymentRepository,
        IEventBus eventBus,
        ILogger<ConfirmPaymentCommandHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<Result<PaymentDto>> Handle(ConfirmPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.GetByIdAsync(request.PaymentId, cancellationToken);
        if (payment == null)
        {
            return Result.Failure<PaymentDto>(Error.NotFound("Payment not found"));
        }

        payment.MarkAsCompleted(request.TransactionReference);
        _paymentRepository.Update(payment);
        await _paymentRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Payment {PaymentId} confirmed with reference {Reference}", payment.Id, request.TransactionReference);

        await _eventBus.PublishAsync(new PaymentCompletedIntegrationEvent(
            payment.Id,
            payment.DeliveryId,
            payment.SenderId,
            payment.TravelerId,
            payment.Amount,
            payment.PlatformFee,
            payment.TravelerPayout,
            payment.TransactionReference!,
            payment.PaidAt!.Value));

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
