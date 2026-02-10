using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.EventBus;
using TagAlong.Package.API.DTOs;
using TagAlong.Package.Domain.Entities;
using TagAlong.Package.Domain.Repositories;

namespace TagAlong.Package.API.Commands;

public record CreatePackageRequestCommand(
    Guid SenderId,
    string PickupLocation,
    double PickupLatitude,
    double PickupLongitude,
    string DeliveryLocation,
    double DeliveryLatitude,
    double DeliveryLongitude,
    string PackageDescription,
    string Size,
    decimal EstimatedWeight,
    decimal? OfferedPrice,
    string? SpecialInstructions,
    DateTime? RequiredByDate,
    string? PackageImageUrl) : ICommand<PackageRequestResponse>;

public class CreatePackageRequestCommandHandler : ICommandHandler<CreatePackageRequestCommand, PackageRequestResponse>
{
    private readonly IPackageRequestRepository _packageRequestRepository;
    private readonly IEventBus _eventBus;

    public CreatePackageRequestCommandHandler(
        IPackageRequestRepository packageRequestRepository,
        IEventBus eventBus)
    {
        _packageRequestRepository = packageRequestRepository;
        _eventBus = eventBus;
    }

    public async Task<Result<PackageRequestResponse>> Handle(CreatePackageRequestCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PackageSize>(request.Size, true, out var size))
        {
            return Result.Failure<PackageRequestResponse>(Error.Validation($"Invalid package size: {request.Size}"));
        }

        var packageRequest = PackageRequest.Create(
            request.SenderId,
            request.PickupLocation,
            request.PickupLatitude,
            request.PickupLongitude,
            request.DeliveryLocation,
            request.DeliveryLatitude,
            request.DeliveryLongitude,
            request.PackageDescription,
            size,
            request.EstimatedWeight,
            request.OfferedPrice,
            request.SpecialInstructions,
            request.RequiredByDate,
            request.PackageImageUrl);

        await _packageRequestRepository.AddAsync(packageRequest, cancellationToken);
        await _packageRequestRepository.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new PackageRequestCreatedIntegrationEvent(
            packageRequest.Id,
            packageRequest.SenderId,
            packageRequest.PickupLocation,
            packageRequest.DeliveryLocation,
            packageRequest.PackageDescription,
            packageRequest.EstimatedWeight,
            packageRequest.OfferedPrice,
            DateTime.UtcNow), cancellationToken);

        return Result.Success(MapToResponse(packageRequest));
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

public record PackageRequestCreatedIntegrationEvent(
    Guid PackageRequestId,
    Guid SenderId,
    string PickupLocation,
    string DeliveryLocation,
    string PackageDescription,
    decimal EstimatedWeight,
    decimal? OfferedPrice,
    DateTime CreatedAt) : IntegrationEvent;
