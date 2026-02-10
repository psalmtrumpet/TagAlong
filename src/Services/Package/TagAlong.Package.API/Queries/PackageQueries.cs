using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.Package.API.DTOs;
using TagAlong.Package.Domain.Entities;
using TagAlong.Package.Domain.Repositories;

namespace TagAlong.Package.API.Queries;

public record SearchPackageRequestsQuery(
    string? PickupLocation,
    string? DeliveryLocation,
    double? PickupLatitude,
    double? PickupLongitude,
    double? DeliveryLatitude,
    double? DeliveryLongitude,
    double RadiusKm,
    int Page,
    int PageSize) : IQuery<List<PackageRequestResponse>>;

public class SearchPackageRequestsQueryHandler : IQueryHandler<SearchPackageRequestsQuery, List<PackageRequestResponse>>
{
    private readonly IPackageRequestRepository _packageRequestRepository;

    public SearchPackageRequestsQueryHandler(IPackageRequestRepository packageRequestRepository)
    {
        _packageRequestRepository = packageRequestRepository;
    }

    public async Task<Result<List<PackageRequestResponse>>> Handle(SearchPackageRequestsQuery request, CancellationToken cancellationToken)
    {
        var packages = await _packageRequestRepository.SearchOpenRequestsAsync(
            request.PickupLocation,
            request.DeliveryLocation,
            request.PickupLatitude,
            request.PickupLongitude,
            request.DeliveryLatitude,
            request.DeliveryLongitude,
            request.RadiusKm,
            request.Page,
            request.PageSize,
            cancellationToken);

        return Result.Success(packages.Select(MapToResponse).ToList());
    }

    private static PackageRequestResponse MapToResponse(PackageRequest request)
    {
        return new PackageRequestResponse(
            request.Id,
            request.SenderId,
            request.PickupLocation,
            request.PickupLatitude,
            request.PickupLongitude,
            request.DeliveryLocation,
            request.DeliveryLatitude,
            request.DeliveryLongitude,
            request.PackageDescription,
            request.Size.ToString(),
            request.EstimatedWeight,
            request.OfferedPrice,
            request.Status.ToString(),
            request.SpecialInstructions,
            request.RequiredByDate,
            request.PackageImageUrl,
            request.CreatedAt);
    }
}

public record GetMyPackageRequestsQuery(Guid SenderId) : IQuery<List<PackageRequestResponse>>;

public class GetMyPackageRequestsQueryHandler : IQueryHandler<GetMyPackageRequestsQuery, List<PackageRequestResponse>>
{
    private readonly IPackageRequestRepository _packageRequestRepository;

    public GetMyPackageRequestsQueryHandler(IPackageRequestRepository packageRequestRepository)
    {
        _packageRequestRepository = packageRequestRepository;
    }

    public async Task<Result<List<PackageRequestResponse>>> Handle(GetMyPackageRequestsQuery request, CancellationToken cancellationToken)
    {
        var packages = await _packageRequestRepository.GetBySenderIdAsync(request.SenderId, cancellationToken);

        return Result.Success(packages.Select(p => new PackageRequestResponse(
            p.Id,
            p.SenderId,
            p.PickupLocation,
            p.PickupLatitude,
            p.PickupLongitude,
            p.DeliveryLocation,
            p.DeliveryLatitude,
            p.DeliveryLongitude,
            p.PackageDescription,
            p.Size.ToString(),
            p.EstimatedWeight,
            p.OfferedPrice,
            p.Status.ToString(),
            p.SpecialInstructions,
            p.RequiredByDate,
            p.PackageImageUrl,
            p.CreatedAt)).ToList());
    }
}

public record GetDeliveryByIdQuery(Guid DeliveryId) : IQuery<DeliveryResponse>;

public class GetDeliveryByIdQueryHandler : IQueryHandler<GetDeliveryByIdQuery, DeliveryResponse>
{
    private readonly IDeliveryRepository _deliveryRepository;

    public GetDeliveryByIdQueryHandler(IDeliveryRepository deliveryRepository)
    {
        _deliveryRepository = deliveryRepository;
    }

    public async Task<Result<DeliveryResponse>> Handle(GetDeliveryByIdQuery request, CancellationToken cancellationToken)
    {
        var delivery = await _deliveryRepository.GetByIdAsync(request.DeliveryId, cancellationToken);

        if (delivery == null)
        {
            return Result.Failure<DeliveryResponse>(Error.NotFound($"Delivery with Id {request.DeliveryId} not found"));
        }

        return Result.Success(new DeliveryResponse(
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
            delivery.CreatedAt));
    }
}

public record GetMyDeliveriesQuery(Guid UserId, string Role) : IQuery<List<DeliveryResponse>>;

public class GetMyDeliveriesQueryHandler : IQueryHandler<GetMyDeliveriesQuery, List<DeliveryResponse>>
{
    private readonly IDeliveryRepository _deliveryRepository;

    public GetMyDeliveriesQueryHandler(IDeliveryRepository deliveryRepository)
    {
        _deliveryRepository = deliveryRepository;
    }

    public async Task<Result<List<DeliveryResponse>>> Handle(GetMyDeliveriesQuery request, CancellationToken cancellationToken)
    {
        var deliveries = request.Role.ToLower() == "traveler"
            ? await _deliveryRepository.GetByTravelerIdAsync(request.UserId, cancellationToken)
            : await _deliveryRepository.GetBySenderIdAsync(request.UserId, cancellationToken);

        return Result.Success(deliveries.Select(d => new DeliveryResponse(
            d.Id,
            d.PackageRequestId,
            d.TripId,
            d.SenderId,
            d.TravelerId,
            d.AgreedPrice,
            d.PlatformFee,
            d.TravelerPayout,
            d.Status.ToString(),
            d.MeetupLocation,
            d.MeetupLatitude,
            d.MeetupLongitude,
            d.MeetupTime,
            d.PickedUpAt,
            d.DeliveredAt,
            d.DeliveryProofImageUrl,
            d.ReceiverName,
            d.ReceiverPhone,
            d.Notes,
            d.CreatedAt)).ToList());
    }
}
