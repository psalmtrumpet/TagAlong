using TagAlong.Common.Domain;

namespace TagAlong.Package.Domain.Entities;

public class PackageRequest : AggregateRoot
{
    public Guid SenderId { get; private set; }
    public string PickupLocation { get; private set; } = null!;
    public double PickupLatitude { get; private set; }
    public double PickupLongitude { get; private set; }
    public string DeliveryLocation { get; private set; } = null!;
    public double DeliveryLatitude { get; private set; }
    public double DeliveryLongitude { get; private set; }
    public string PackageDescription { get; private set; } = null!;
    public PackageSize Size { get; private set; }
    public decimal EstimatedWeight { get; private set; }
    public decimal? OfferedPrice { get; private set; }
    public PackageRequestStatus Status { get; private set; } = PackageRequestStatus.Open;
    public string? SpecialInstructions { get; private set; }
    public DateTime? RequiredByDate { get; private set; }
    public string? PackageImageUrl { get; private set; }

    private PackageRequest() { }

    public static PackageRequest Create(
        Guid senderId,
        string pickupLocation,
        double pickupLatitude,
        double pickupLongitude,
        string deliveryLocation,
        double deliveryLatitude,
        double deliveryLongitude,
        string packageDescription,
        PackageSize size,
        decimal estimatedWeight,
        decimal? offeredPrice,
        string? specialInstructions,
        DateTime? requiredByDate,
        string? packageImageUrl)
    {
        return new PackageRequest
        {
            SenderId = senderId,
            PickupLocation = pickupLocation,
            PickupLatitude = pickupLatitude,
            PickupLongitude = pickupLongitude,
            DeliveryLocation = deliveryLocation,
            DeliveryLatitude = deliveryLatitude,
            DeliveryLongitude = deliveryLongitude,
            PackageDescription = packageDescription,
            Size = size,
            EstimatedWeight = estimatedWeight,
            OfferedPrice = offeredPrice,
            SpecialInstructions = specialInstructions,
            RequiredByDate = requiredByDate,
            PackageImageUrl = packageImageUrl
        };
    }

    public void MarkAsMatched()
    {
        if (Status != PackageRequestStatus.Open)
            throw new InvalidOperationException("Only open package requests can be matched");

        Status = PackageRequestStatus.Matched;
        SetUpdated();
    }

    public void Cancel()
    {
        if (Status == PackageRequestStatus.Delivered)
            throw new InvalidOperationException("Cannot cancel a delivered package");

        Status = PackageRequestStatus.Cancelled;
        SetUpdated();
    }

    public void Reopen()
    {
        if (Status != PackageRequestStatus.Cancelled)
            throw new InvalidOperationException("Only cancelled requests can be reopened");

        Status = PackageRequestStatus.Open;
        SetUpdated();
    }

    public void UpdateOfferedPrice(decimal price)
    {
        OfferedPrice = price;
        SetUpdated();
    }
}

public enum PackageRequestStatus
{
    Open,
    Matched,
    InTransit,
    Delivered,
    Cancelled
}

public enum PackageSize
{
    Small,
    Medium,
    Large,
    ExtraLarge
}
