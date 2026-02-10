using FluentValidation;
using TagAlong.User.API.DTOs;

namespace TagAlong.User.API.Validators;

public class SetAvailabilityRequestValidator : AbstractValidator<SetAvailabilityRequest>
{
    public SetAvailabilityRequestValidator()
    {
        When(x => x.IsAvailable, () =>
        {
            RuleFor(x => x.Latitude)
                .NotNull().WithMessage("Latitude is required when becoming available")
                .InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue)
                .WithMessage("Latitude must be between -90 and 90");

            RuleFor(x => x.Longitude)
                .NotNull().WithMessage("Longitude is required when becoming available")
                .InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue)
                .WithMessage("Longitude must be between -180 and 180");

            RuleFor(x => x.DurationMinutes)
                .InclusiveBetween(15, 480).When(x => x.DurationMinutes.HasValue)
                .WithMessage("Duration must be between 15 minutes and 8 hours");
        });

        RuleFor(x => x.LocationName)
            .MaximumLength(256).When(x => !string.IsNullOrEmpty(x.LocationName))
            .WithMessage("Location name cannot exceed 256 characters");
    }
}

public class UpdateLocationRequestValidator : AbstractValidator<UpdateLocationRequest>
{
    public UpdateLocationRequestValidator()
    {
        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90)
            .WithMessage("Latitude must be between -90 and 90");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180)
            .WithMessage("Longitude must be between -180 and 180");

        RuleFor(x => x.LocationName)
            .MaximumLength(256).When(x => !string.IsNullOrEmpty(x.LocationName))
            .WithMessage("Location name cannot exceed 256 characters");
    }
}

public class SearchAvailableUsersRequestValidator : AbstractValidator<SearchAvailableUsersRequest>
{
    public SearchAvailableUsersRequestValidator()
    {
        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90)
            .WithMessage("Latitude must be between -90 and 90");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180)
            .WithMessage("Longitude must be between -180 and 180");

        RuleFor(x => x.RadiusKm)
            .InclusiveBetween(0.5, 50).When(x => x.RadiusKm.HasValue)
            .WithMessage("Radius must be between 0.5 and 50 km");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).When(x => x.Page.HasValue)
            .WithMessage("Page must be at least 1");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).When(x => x.PageSize.HasValue)
            .WithMessage("Page size must be between 1 and 100");
    }
}

public class UpdateLocationPreferencesRequestValidator : AbstractValidator<UpdateLocationPreferencesRequest>
{
    public UpdateLocationPreferencesRequestValidator()
    {
        RuleFor(x => x.MaxTravelRadiusKm)
            .InclusiveBetween(1, 100)
            .WithMessage("Max travel radius must be between 1 and 100 km");
    }
}
