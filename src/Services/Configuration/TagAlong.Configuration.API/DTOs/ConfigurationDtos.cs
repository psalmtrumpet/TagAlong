namespace TagAlong.Configuration.API.DTOs;

public record PlatformConfigurationDto(
    Guid Id,
    string Key,
    string Value,
    string Description,
    string Type,
    bool IsActive,
    DateTime CreatedAt);

public record FeeConfigurationDto(
    Guid Id,
    string Name,
    decimal MinPercentage,
    decimal MaxPercentage,
    decimal DefaultPercentage,
    string Description,
    bool IsActive,
    DateTime CreatedAt);

public record CreateConfigurationRequest(
    string Key,
    string Value,
    string Description,
    string Type);

public record UpdateConfigurationRequest(string Value);

public record CreateFeeConfigurationRequest(
    string Name,
    decimal MinPercentage,
    decimal MaxPercentage,
    decimal DefaultPercentage,
    string Description);

public record UpdateFeeConfigurationRequest(
    decimal MinPercentage,
    decimal MaxPercentage,
    decimal DefaultPercentage);
