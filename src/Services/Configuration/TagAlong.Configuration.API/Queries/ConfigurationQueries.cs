using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.Configuration.API.DTOs;
using TagAlong.Configuration.Domain.Entities;
using TagAlong.Configuration.Domain.Repositories;

namespace TagAlong.Configuration.API.Queries;

// Platform Configuration Queries
public record GetAllConfigurationsQuery(int Page, int PageSize) : IQuery<IEnumerable<PlatformConfigurationDto>>;
public record GetActiveConfigurationsQuery() : IQuery<IEnumerable<PlatformConfigurationDto>>;
public record GetConfigurationByKeyQuery(string Key) : IQuery<PlatformConfigurationDto?>;
public record GetConfigurationsByTypeQuery(ConfigurationType Type) : IQuery<IEnumerable<PlatformConfigurationDto>>;

public class GetAllConfigurationsQueryHandler : IQueryHandler<GetAllConfigurationsQuery, IEnumerable<PlatformConfigurationDto>>
{
    private readonly IPlatformConfigurationRepository _repository;

    public GetAllConfigurationsQueryHandler(IPlatformConfigurationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IEnumerable<PlatformConfigurationDto>>> Handle(GetAllConfigurationsQuery request, CancellationToken cancellationToken)
    {
        var configs = await _repository.GetAllAsync(request.Page, request.PageSize, cancellationToken);
        return Result.Success(configs.Select(MapToDto));
    }

    private static PlatformConfigurationDto MapToDto(PlatformConfiguration c) => new(
        c.Id, c.Key, c.Value, c.Description, c.Type.ToString(), c.IsActive, c.CreatedAt);
}

public class GetActiveConfigurationsQueryHandler : IQueryHandler<GetActiveConfigurationsQuery, IEnumerable<PlatformConfigurationDto>>
{
    private readonly IPlatformConfigurationRepository _repository;

    public GetActiveConfigurationsQueryHandler(IPlatformConfigurationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IEnumerable<PlatformConfigurationDto>>> Handle(GetActiveConfigurationsQuery request, CancellationToken cancellationToken)
    {
        var configs = await _repository.GetAllActiveAsync(cancellationToken);
        return Result.Success(configs.Select(MapToDto));
    }

    private static PlatformConfigurationDto MapToDto(PlatformConfiguration c) => new(
        c.Id, c.Key, c.Value, c.Description, c.Type.ToString(), c.IsActive, c.CreatedAt);
}

public class GetConfigurationByKeyQueryHandler : IQueryHandler<GetConfigurationByKeyQuery, PlatformConfigurationDto?>
{
    private readonly IPlatformConfigurationRepository _repository;

    public GetConfigurationByKeyQueryHandler(IPlatformConfigurationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<PlatformConfigurationDto?>> Handle(GetConfigurationByKeyQuery request, CancellationToken cancellationToken)
    {
        var config = await _repository.GetByKeyAsync(request.Key, cancellationToken);
        if (config == null) return Result.Success<PlatformConfigurationDto?>(null);

        return Result.Success<PlatformConfigurationDto?>(MapToDto(config));
    }

    private static PlatformConfigurationDto MapToDto(PlatformConfiguration c) => new(
        c.Id, c.Key, c.Value, c.Description, c.Type.ToString(), c.IsActive, c.CreatedAt);
}

public class GetConfigurationsByTypeQueryHandler : IQueryHandler<GetConfigurationsByTypeQuery, IEnumerable<PlatformConfigurationDto>>
{
    private readonly IPlatformConfigurationRepository _repository;

    public GetConfigurationsByTypeQueryHandler(IPlatformConfigurationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IEnumerable<PlatformConfigurationDto>>> Handle(GetConfigurationsByTypeQuery request, CancellationToken cancellationToken)
    {
        var configs = await _repository.GetByTypeAsync(request.Type, cancellationToken);
        return Result.Success(configs.Select(MapToDto));
    }

    private static PlatformConfigurationDto MapToDto(PlatformConfiguration c) => new(
        c.Id, c.Key, c.Value, c.Description, c.Type.ToString(), c.IsActive, c.CreatedAt);
}

// Fee Configuration Queries
public record GetAllFeeConfigurationsQuery(int Page, int PageSize) : IQuery<IEnumerable<FeeConfigurationDto>>;
public record GetActiveFeeConfigurationQuery() : IQuery<FeeConfigurationDto?>;
public record GetFeeConfigurationByIdQuery(Guid Id) : IQuery<FeeConfigurationDto?>;

public class GetAllFeeConfigurationsQueryHandler : IQueryHandler<GetAllFeeConfigurationsQuery, IEnumerable<FeeConfigurationDto>>
{
    private readonly IFeeConfigurationRepository _repository;

    public GetAllFeeConfigurationsQueryHandler(IFeeConfigurationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IEnumerable<FeeConfigurationDto>>> Handle(GetAllFeeConfigurationsQuery request, CancellationToken cancellationToken)
    {
        var configs = await _repository.GetAllAsync(request.Page, request.PageSize, cancellationToken);
        return Result.Success(configs.Select(MapToDto));
    }

    private static FeeConfigurationDto MapToDto(FeeConfiguration f) => new(
        f.Id, f.Name, f.MinPercentage, f.MaxPercentage, f.DefaultPercentage, f.Description, f.IsActive, f.CreatedAt);
}

public class GetActiveFeeConfigurationQueryHandler : IQueryHandler<GetActiveFeeConfigurationQuery, FeeConfigurationDto?>
{
    private readonly IFeeConfigurationRepository _repository;

    public GetActiveFeeConfigurationQueryHandler(IFeeConfigurationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<FeeConfigurationDto?>> Handle(GetActiveFeeConfigurationQuery request, CancellationToken cancellationToken)
    {
        var config = await _repository.GetActiveAsync(cancellationToken);
        if (config == null) return Result.Success<FeeConfigurationDto?>(null);

        return Result.Success<FeeConfigurationDto?>(MapToDto(config));
    }

    private static FeeConfigurationDto MapToDto(FeeConfiguration f) => new(
        f.Id, f.Name, f.MinPercentage, f.MaxPercentage, f.DefaultPercentage, f.Description, f.IsActive, f.CreatedAt);
}

public class GetFeeConfigurationByIdQueryHandler : IQueryHandler<GetFeeConfigurationByIdQuery, FeeConfigurationDto?>
{
    private readonly IFeeConfigurationRepository _repository;

    public GetFeeConfigurationByIdQueryHandler(IFeeConfigurationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<FeeConfigurationDto?>> Handle(GetFeeConfigurationByIdQuery request, CancellationToken cancellationToken)
    {
        var config = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (config == null) return Result.Success<FeeConfigurationDto?>(null);

        return Result.Success<FeeConfigurationDto?>(MapToDto(config));
    }

    private static FeeConfigurationDto MapToDto(FeeConfiguration f) => new(
        f.Id, f.Name, f.MinPercentage, f.MaxPercentage, f.DefaultPercentage, f.Description, f.IsActive, f.CreatedAt);
}
