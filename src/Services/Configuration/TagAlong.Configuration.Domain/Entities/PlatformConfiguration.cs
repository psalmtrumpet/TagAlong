using TagAlong.Common.Domain;

namespace TagAlong.Configuration.Domain.Entities;

public class PlatformConfiguration : Entity
{
    public string Key { get; private set; } = null!;
    public string Value { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public ConfigurationType Type { get; private set; }
    public bool IsActive { get; private set; }

    private PlatformConfiguration() { }

    public static PlatformConfiguration Create(
        string key,
        string value,
        string description,
        ConfigurationType type)
    {
        return new PlatformConfiguration
        {
            Key = key,
            Value = value,
            Description = description,
            Type = type,
            IsActive = true
        };
    }

    public void UpdateValue(string value)
    {
        Value = value;
        SetUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        SetUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdated();
    }
}

public enum ConfigurationType
{
    PlatformFee,
    PaymentGateway,
    Notification,
    Security,
    Feature,
    Other
}
