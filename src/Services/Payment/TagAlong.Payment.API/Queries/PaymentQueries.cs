using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.Payment.API.DTOs;
using TagAlong.Payment.Domain.Entities;
using TagAlong.Payment.Domain.Repositories;

namespace TagAlong.Payment.API.Queries;

public record GetPaymentByIdQuery(Guid PaymentId) : IQuery<PaymentDto?>;

public class GetPaymentByIdQueryHandler : IQueryHandler<GetPaymentByIdQuery, PaymentDto?>
{
    private readonly IPaymentRepository _paymentRepository;

    public GetPaymentByIdQueryHandler(IPaymentRepository paymentRepository)
    {
        _paymentRepository = paymentRepository;
    }

    public async Task<Result<PaymentDto?>> Handle(GetPaymentByIdQuery request, CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.GetByIdAsync(request.PaymentId, cancellationToken);
        return Result.Success(payment == null ? null : MapToDto(payment));
    }

    private static PaymentDto MapToDto(Domain.Entities.Payment payment) => new(
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
        payment.CreatedAt);
}

public record GetPaymentByDeliveryIdQuery(Guid DeliveryId) : IQuery<PaymentDto?>;

public class GetPaymentByDeliveryIdQueryHandler : IQueryHandler<GetPaymentByDeliveryIdQuery, PaymentDto?>
{
    private readonly IPaymentRepository _paymentRepository;

    public GetPaymentByDeliveryIdQueryHandler(IPaymentRepository paymentRepository)
    {
        _paymentRepository = paymentRepository;
    }

    public async Task<Result<PaymentDto?>> Handle(GetPaymentByDeliveryIdQuery request, CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.GetByDeliveryIdAsync(request.DeliveryId, cancellationToken);
        return Result.Success(payment == null ? null : MapToDto(payment));
    }

    private static PaymentDto MapToDto(Domain.Entities.Payment payment) => new(
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
        payment.CreatedAt);
}

public record GetPaymentsBySenderQuery(Guid SenderId, int Page, int PageSize) : IQuery<IEnumerable<PaymentDto>>;

public class GetPaymentsBySenderQueryHandler : IQueryHandler<GetPaymentsBySenderQuery, IEnumerable<PaymentDto>>
{
    private readonly IPaymentRepository _paymentRepository;

    public GetPaymentsBySenderQueryHandler(IPaymentRepository paymentRepository)
    {
        _paymentRepository = paymentRepository;
    }

    public async Task<Result<IEnumerable<PaymentDto>>> Handle(GetPaymentsBySenderQuery request, CancellationToken cancellationToken)
    {
        var payments = await _paymentRepository.GetBySenderIdAsync(request.SenderId, request.Page, request.PageSize, cancellationToken);
        return Result.Success(payments.Select(MapToDto));
    }

    private static PaymentDto MapToDto(Domain.Entities.Payment payment) => new(
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
        payment.CreatedAt);
}

public record GetPaymentsByTravelerQuery(Guid TravelerId, int Page, int PageSize) : IQuery<IEnumerable<PaymentDto>>;

public class GetPaymentsByTravelerQueryHandler : IQueryHandler<GetPaymentsByTravelerQuery, IEnumerable<PaymentDto>>
{
    private readonly IPaymentRepository _paymentRepository;

    public GetPaymentsByTravelerQueryHandler(IPaymentRepository paymentRepository)
    {
        _paymentRepository = paymentRepository;
    }

    public async Task<Result<IEnumerable<PaymentDto>>> Handle(GetPaymentsByTravelerQuery request, CancellationToken cancellationToken)
    {
        var payments = await _paymentRepository.GetByTravelerIdAsync(request.TravelerId, request.Page, request.PageSize, cancellationToken);
        return Result.Success(payments.Select(MapToDto));
    }

    private static PaymentDto MapToDto(Domain.Entities.Payment payment) => new(
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
        payment.CreatedAt);
}

public record GetTravelerEarningsQuery(Guid TravelerId) : IQuery<decimal>;

public class GetTravelerEarningsQueryHandler : IQueryHandler<GetTravelerEarningsQuery, decimal>
{
    private readonly IPaymentRepository _paymentRepository;

    public GetTravelerEarningsQueryHandler(IPaymentRepository paymentRepository)
    {
        _paymentRepository = paymentRepository;
    }

    public async Task<Result<decimal>> Handle(GetTravelerEarningsQuery request, CancellationToken cancellationToken)
    {
        var earnings = await _paymentRepository.GetTotalEarningsByTravelerAsync(request.TravelerId, cancellationToken);
        return Result.Success(earnings);
    }
}

public record GetSenderSpendingQuery(Guid SenderId) : IQuery<decimal>;

public class GetSenderSpendingQueryHandler : IQueryHandler<GetSenderSpendingQuery, decimal>
{
    private readonly IPaymentRepository _paymentRepository;

    public GetSenderSpendingQueryHandler(IPaymentRepository paymentRepository)
    {
        _paymentRepository = paymentRepository;
    }

    public async Task<Result<decimal>> Handle(GetSenderSpendingQuery request, CancellationToken cancellationToken)
    {
        var spending = await _paymentRepository.GetTotalSpentBySenderAsync(request.SenderId, cancellationToken);
        return Result.Success(spending);
    }
}
