using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.EventBus;
using TagAlong.Package.API.DTOs;
using TagAlong.Package.Domain.Entities;
using TagAlong.Package.Domain.Repositories;

namespace TagAlong.Package.API.Commands;

public record UpdateDeliveryStatusCommand(
    Guid DeliveryId,
    Guid UserId,
    string NewStatus,
    string? ProofImageUrl = null) : ICommand<DeliveryResponse>;

public class UpdateDeliveryStatusCommandHandler : ICommandHandler<UpdateDeliveryStatusCommand, DeliveryResponse>
{
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly IEventBus _eventBus;

    public UpdateDeliveryStatusCommandHandler(IDeliveryRepository deliveryRepository, IEventBus eventBus)
    {
        _deliveryRepository = deliveryRepository;
        _eventBus = eventBus;
    }

    public async Task<Result<DeliveryResponse>> Handle(UpdateDeliveryStatusCommand request, CancellationToken cancellationToken)
    {
        var delivery = await _deliveryRepository.GetByIdAsync(request.DeliveryId, cancellationToken);

        if (delivery == null)
        {
            return Result.Failure<DeliveryResponse>(Error.NotFound($"Delivery with Id {request.DeliveryId} not found"));
        }

        if (delivery.SenderId != request.UserId && delivery.TravelerId != request.UserId)
        {
            return Result.Failure<DeliveryResponse>(Error.Unauthorized("You don't have permission to update this delivery"));
        }

        var oldStatus = delivery.Status.ToString();

        switch (request.NewStatus.ToLower())
        {
            case "accept":
            case "accepted":
                if (delivery.TravelerId != request.UserId)
                    return Result.Failure<DeliveryResponse>(Error.Unauthorized("Only the traveler can accept the delivery"));
                delivery.Accept();
                break;
            case "reject":
            case "rejected":
                if (delivery.TravelerId != request.UserId)
                    return Result.Failure<DeliveryResponse>(Error.Unauthorized("Only the traveler can reject the delivery"));
                delivery.Reject();
                break;
            case "pickedup":
                if (delivery.TravelerId != request.UserId)
                    return Result.Failure<DeliveryResponse>(Error.Unauthorized("Only the traveler can mark as picked up"));
                delivery.MarkAsPickedUp();
                break;
            case "intransit":
                if (delivery.TravelerId != request.UserId)
                    return Result.Failure<DeliveryResponse>(Error.Unauthorized("Only the traveler can mark as in transit"));
                delivery.MarkAsInTransit();
                break;
            case "delivered":
                if (delivery.TravelerId != request.UserId)
                    return Result.Failure<DeliveryResponse>(Error.Unauthorized("Only the traveler can mark as delivered"));
                delivery.MarkAsDelivered(request.ProofImageUrl);
                break;
            case "cancel":
            case "cancelled":
                delivery.Cancel();
                break;
            default:
                return Result.Failure<DeliveryResponse>(Error.Validation($"Invalid status: {request.NewStatus}"));
        }

        _deliveryRepository.Update(delivery);
        await _deliveryRepository.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new DeliveryStatusChangedIntegrationEvent(
            delivery.Id,
            delivery.PackageRequestId,
            delivery.SenderId,
            delivery.TravelerId,
            oldStatus,
            delivery.Status.ToString(),
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

public record DeliveryStatusChangedIntegrationEvent(
    Guid DeliveryId,
    Guid PackageRequestId,
    Guid SenderId,
    Guid TravelerId,
    string OldStatus,
    string NewStatus,
    DateTime ChangedAt) : IntegrationEvent;
