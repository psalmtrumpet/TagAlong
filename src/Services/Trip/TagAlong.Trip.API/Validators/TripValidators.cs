using FluentValidation;
using TagAlong.Trip.API.Commands;
using TagAlong.Trip.API.Queries;

namespace TagAlong.Trip.API.Validators;

public class CreateTripCommandValidator : AbstractValidator<CreateTripCommand>
{
    public CreateTripCommandValidator()
    {
        RuleFor(x => x.Origin).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Destination).NotEmpty().MaximumLength(200);
        RuleFor(x => x.OriginLatitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.OriginLongitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.DestinationLatitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.DestinationLongitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.DepartureTime).GreaterThan(DateTime.UtcNow);
        RuleFor(x => x.AvailableCapacity).GreaterThan(0).LessThanOrEqualTo(10000);
        RuleFor(x => x.MaxPackages).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100);
        RuleFor(x => x.VehiclePlateNumber).MaximumLength(20).When(x => x.VehiclePlateNumber != null);
        RuleFor(x => x.Notes).MaximumLength(500).When(x => x.Notes != null);
        RuleFor(x => x.TripType).Must(t => t == "Passenger" || t == "Delivery")
            .WithMessage("TripType must be 'Passenger' or 'Delivery'");
    }
}

public class SearchTripsQueryValidator : AbstractValidator<SearchTripsQuery>
{
    public SearchTripsQueryValidator()
    {
        RuleFor(x => x.RadiusKm).GreaterThan(0).LessThanOrEqualTo(500);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Origin).MaximumLength(200).When(x => x.Origin != null);
        RuleFor(x => x.Destination).MaximumLength(200).When(x => x.Destination != null);
        RuleFor(x => x.OriginLatitude).InclusiveBetween(-90, 90).When(x => x.OriginLatitude.HasValue);
        RuleFor(x => x.OriginLongitude).InclusiveBetween(-180, 180).When(x => x.OriginLongitude.HasValue);
        RuleFor(x => x.MaxDetourSeconds).GreaterThan(0).LessThanOrEqualTo(7200);
    }
}
