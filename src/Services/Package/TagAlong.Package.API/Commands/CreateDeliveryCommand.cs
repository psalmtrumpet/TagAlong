using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.EventBus;
using TagAlong.Package.API.DTOs;
using TagAlong.Package.Domain.Entities;
using TagAlong.Package.Domain.Repositories;

namespace TagAlong.Package.API.Commands;

public record CreateDeliveryCommand(
    Guid TravelerId,
    Guid PackageRequestId,
    Guid TripId,
    decimal AgreedPrice,
    string? MeetupLocation,
    double? MeetupLatitude,
    double? MeetupLongitude,
    DateTime? MeetupTime,
    string? ReceiverName,
    string? ReceiverPhone) : ICommand<DeliveryResponse>;

public class CreateDeliveryCommandHandler : ICommandHandler<CreateDeliveryCommand, DeliveryResponse>
{
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly IPackageRequestRepository _packageRequestRepository;
    private readonly IEventBus _eventBus;
    private readonly IConfiguration _configuration;

    public CreateDeliveryCommandHandler(
        IDeliveryRepository deliveryRepository,
        IPackageRequestRepository packageRequestRepository,
        IEventBus eventBus,
        IConfiguration configuration)
    {
        _deliveryRepository = deliveryRepository;
        _packageRequestRepository = packageRequestRepository;
        _eventBus = eventBus;
        _configuration = configuration;
    }

    public async Task<Result<DeliveryResponse>> Handle(CreateDeliveryCommand request, CancellationToken cancellationToken)
    {
        var packageRequest = await _packageRequestRepository.GetByIdAsync(request.PackageRequestId, cancellationToken);

        if (packageRequest == null)
        {
            return Result.Failure<DeliveryResponse>(Error.NotFound($"PackageRequest with Id {request.PackageRequestId} not found"));
        }

        if (packageRequest.Status != PackageRequestStatus.Open)
        {
            return Result.Failure<DeliveryResponse>(Error.Validation("Package request is not open for delivery"));
        }

        var existingDelivery = await _deliveryRepository.GetByPackageRequestIdAsync(request.PackageRequestId, cancellationToken);
        if (existingDelivery != null)
        {
            return Result.Failure<DeliveryResponse>(Error.Conflict("A delivery already exists for this package request"));
        }

        var platformFeePercentage = decimal.Parse(_configuration["PlatformSettings:FeePercentage"] ?? "10");

        var delivery = Delivery.Create(
            request.PackageRequestId,
            request.TripId,
            packageRequest.SenderId,
            request.TravelerId,
            request.AgreedPrice,
            platformFeePercentage,
            request.MeetupLocation,
            request.MeetupLatitude,
            request.MeetupLongitude,
            request.MeetupTime,
            request.ReceiverName,
            request.ReceiverPhone);

        packageRequest.MarkAsMatched();

        await _deliveryRepository.AddAsync(delivery, cancellationToken);
        _packageRequestRepository.Update(packageRequest);
        await _deliveryRepository.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new DeliveryMatchedIntegrationEvent(
            delivery.Id,
            delivery.PackageRequestId,
            delivery.TripId,
            delivery.SenderId,
            delivery.TravelerId,
            delivery.AgreedPrice,
            DateTime.UtcNow), cancellationToken);

        return Result.Success(MapToResponse(delivery));
    }

    private static DeliveryResponse MapToResponse(Delivery delivery)
    {
        return new DeliveryResponse(
            delivery.Id,
            delivery.PackageRequestId,
            delivery.TripId,
            delivery.SenderId,
            delivery.TravelerId,
            delivery.AgreedPrice,
            delivery.PlatformFee,
            delivery.TravelerPayout,
            delivery.Status.ToString(),
            delivery.MeetupLocation,
            delivery.MeetupLatitude,
            delivery.MeetupLongitude,
            delivery.MeetupTime,
            delivery.PickedUpAt,
            delivery.DeliveredAt,
            delivery.DeliveryProofImageUrl,
            delivery.ReceiverName,
            delivery.ReceiverPhone,
            delivery.Notes,
            delivery.CreatedAt);
    }
}

public record DeliveryMatchedIntegrationEvent(
    Guid DeliveryId,
    Guid PackageRequestId,
    Guid TripId,
    Guid SenderId,
    Guid TravelerId,
    decimal AgreedPrice,
    DateTime MatchedAt) : IntegrationEvent;
