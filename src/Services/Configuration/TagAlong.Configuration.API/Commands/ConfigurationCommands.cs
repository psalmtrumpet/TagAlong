using FluentValidation;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.Configuration.API.DTOs;
using TagAlong.Configuration.Domain.Entities;
using TagAlong.Configuration.Domain.Repositories;

namespace TagAlong.Configuration.API.Commands;

// Platform Configuration Commands
public record CreateConfigurationCommand(
    string Key,
    string Value,
    string Description,
    string Type) : ICommand<PlatformConfigurationDto>;

public class CreateConfigurationCommandValidator : AbstractValidator<CreateConfigurationCommand>
{
    public CreateConfigurationCommandValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Value).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Type).NotEmpty();
    }
}

public class CreateConfigurationCommandHandler : ICommandHandler<CreateConfigurationCommand, PlatformConfigurationDto>
{
    private readonly IPlatformConfigurationRepository _repository;

    public CreateConfigurationCommandHandler(IPlatformConfigurationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<PlatformConfigurationDto>> Handle(CreateConfigurationCommand request, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByKeyAsync(request.Key, cancellationToken);
        if (existing != null)
            return Result.Failure<PlatformConfigurationDto>(Error.Conflict($"Configuration with key '{request.Key}' already exists"));

        if (!Enum.TryParse<ConfigurationType>(request.Type, true, out var type))
            type = ConfigurationType.Other;

        var config = PlatformConfiguration.Create(request.Key, request.Value, request.Description, type);
        await _repository.AddAsync(config, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result.Success(MapToDto(config));
    }

    private static PlatformConfigurationDto MapToDto(PlatformConfiguration c) => new(
        c.Id, c.Key, c.Value, c.Description, c.Type.ToString(), c.IsActive, c.CreatedAt);
}

public record UpdateConfigurationCommand(Guid Id, string Value) : ICommand<PlatformConfigurationDto?>;

public class UpdateConfigurationCommandHandler : ICommandHandler<UpdateConfigurationCommand, PlatformConfigurationDto?>
{
    private readonly IPlatformConfigurationRepository _repository;

    public UpdateConfigurationCommandHandler(IPlatformConfigurationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<PlatformConfigurationDto?>> Handle(UpdateConfigurationCommand request, CancellationToken cancellationToken)
    {
        var config = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (config == null) return Result.Success<PlatformConfigurationDto?>(null);

        config.UpdateValue(request.Value);
        _repository.Update(config);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result.Success<PlatformConfigurationDto?>(MapToDto(config));
    }

    private static PlatformConfigurationDto MapToDto(PlatformConfiguration c) => new(
        c.Id, c.Key, c.Value, c.Description, c.Type.ToString(), c.IsActive, c.CreatedAt);
}

public record ActivateConfigurationCommand(Guid Id) : ICommand<bool>;

public class ActivateConfigurationCommandHandler : ICommandHandler<ActivateConfigurationCommand, bool>
{
    private readonly IPlatformConfigurationRepository _repository;

    public ActivateConfigurationCommandHandler(IPlatformConfigurationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<bool>> Handle(ActivateConfigurationCommand request, CancellationToken cancellationToken)
    {
        var config = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (config == null) return Result.Failure<bool>(Error.NotFound("Configuration not found"));

        config.Activate();
        _repository.Update(config);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}

public record DeactivateConfigurationCommand(Guid Id) : ICommand<bool>;

public class DeactivateConfigurationCommandHandler : ICommandHandler<DeactivateConfigurationCommand, bool>
{
    private readonly IPlatformConfigurationRepository _repository;

    public DeactivateConfigurationCommandHandler(IPlatformConfigurationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<bool>> Handle(DeactivateConfigurationCommand request, CancellationToken cancellationToken)
    {
        var config = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (config == null) return Result.Failure<bool>(Error.NotFound("Configuration not found"));

        config.Deactivate();
        _repository.Update(config);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}

public record DeleteConfigurationCommand(Guid Id) : ICommand<bool>;

public class DeleteConfigurationCommandHandler : ICommandHandler<DeleteConfigurationCommand, bool>
{
    private readonly IPlatformConfigurationRepository _repository;

    public DeleteConfigurationCommandHandler(IPlatformConfigurationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<bool>> Handle(DeleteConfigurationCommand request, CancellationToken cancellationToken)
    {
        var config = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (config == null) return Result.Failure<bool>(Error.NotFound("Configuration not found"));

        _repository.Delete(config);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}

// Fee Configuration Commands
public record CreateFeeConfigurationCommand(
    string Name,
    decimal MinPercentage,
    decimal MaxPercentage,
    decimal DefaultPercentage,
    string Description) : ICommand<FeeConfigurationDto>;

public class CreateFeeConfigurationCommandValidator : AbstractValidator<CreateFeeConfigurationCommand>
{
    public CreateFeeConfigurationCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MinPercentage).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100);
        RuleFor(x => x.MaxPercentage).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100);
        RuleFor(x => x.DefaultPercentage).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
    }
}

public class CreateFeeConfigurationCommandHandler : ICommandHandler<CreateFeeConfigurationCommand, FeeConfigurationDto>
{
    private readonly IFeeConfigurationRepository _repository;

    public CreateFeeConfigurationCommandHandler(IFeeConfigurationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<FeeConfigurationDto>> Handle(CreateFeeConfigurationCommand request, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByNameAsync(request.Name, cancellationToken);
        if (existing != null)
            return Result.Failure<FeeConfigurationDto>(Error.Conflict($"Fee configuration with name '{request.Name}' already exists"));

        var config = FeeConfiguration.Create(
            request.Name,
            request.MinPercentage,
            request.MaxPercentage,
            request.DefaultPercentage,
            request.Description);

        await _repository.AddAsync(config, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result.Success(MapToDto(config));
    }

    private static FeeConfigurationDto MapToDto(FeeConfiguration f) => new(
        f.Id, f.Name, f.MinPercentage, f.MaxPercentage, f.DefaultPercentage, f.Description, f.IsActive, f.CreatedAt);
}

public record UpdateFeeConfigurationCommand(
    Guid Id,
    decimal MinPercentage,
    decimal MaxPercentage,
    decimal DefaultPercentage) : ICommand<FeeConfigurationDto?>;

public class UpdateFeeConfigurationCommandHandler : ICommandHandler<UpdateFeeConfigurationCommand, FeeConfigurationDto?>
{
    private readonly IFeeConfigurationRepository _repository;

    public UpdateFeeConfigurationCommandHandler(IFeeConfigurationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<FeeConfigurationDto?>> Handle(UpdateFeeConfigurationCommand request, CancellationToken cancellationToken)
    {
        var config = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (config == null) return Result.Success<FeeConfigurationDto?>(null);

        config.UpdatePercentages(request.MinPercentage, request.MaxPercentage, request.DefaultPercentage);
        _repository.Update(config);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result.Success<FeeConfigurationDto?>(MapToDto(config));
    }

    private static FeeConfigurationDto MapToDto(FeeConfiguration f) => new(
        f.Id, f.Name, f.MinPercentage, f.MaxPercentage, f.DefaultPercentage, f.Description, f.IsActive, f.CreatedAt);
}

public record ActivateFeeConfigurationCommand(Guid Id) : ICommand<bool>;

public class ActivateFeeConfigurationCommandHandler : ICommandHandler<ActivateFeeConfigurationCommand, bool>
{
    private readonly IFeeConfigurationRepository _repository;

    public ActivateFeeConfigurationCommandHandler(IFeeConfigurationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<bool>> Handle(ActivateFeeConfigurationCommand request, CancellationToken cancellationToken)
    {
        var config = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (config == null) return Result.Failure<bool>(Error.NotFound("Fee configuration not found"));

        config.Activate();
        _repository.Update(config);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}

public record DeactivateFeeConfigurationCommand(Guid Id) : ICommand<bool>;

public class DeactivateFeeConfigurationCommandHandler : ICommandHandler<DeactivateFeeConfigurationCommand, bool>
{
    private readonly IFeeConfigurationRepository _repository;

    public DeactivateFeeConfigurationCommandHandler(IFeeConfigurationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<bool>> Handle(DeactivateFeeConfigurationCommand request, CancellationToken cancellationToken)
    {
        var config = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (config == null) return Result.Failure<bool>(Error.NotFound("Fee configuration not found"));

        config.Deactivate();
        _repository.Update(config);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}

public record DeleteFeeConfigurationCommand(Guid Id) : ICommand<bool>;

public class DeleteFeeConfigurationCommandHandler : ICommandHandler<DeleteFeeConfigurationCommand, bool>
{
    private readonly IFeeConfigurationRepository _repository;

    public DeleteFeeConfigurationCommandHandler(IFeeConfigurationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<bool>> Handle(DeleteFeeConfigurationCommand request, CancellationToken cancellationToken)
    {
        var config = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (config == null) return Result.Failure<bool>(Error.NotFound("Fee configuration not found"));

        _repository.Delete(config);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
