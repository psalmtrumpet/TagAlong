using TagAlong.Common.Domain;

namespace TagAlong.Configuration.Domain.Entities;

public class FeeConfiguration : Entity
{
    public string Name { get; private set; } = null!;
    public decimal MinPercentage { get; private set; }
    public decimal MaxPercentage { get; private set; }
    public decimal DefaultPercentage { get; private set; }
    public string Description { get; private set; } = null!;
    public bool IsActive { get; private set; }

    private FeeConfiguration() { }

    public static FeeConfiguration Create(
        string name,
        decimal minPercentage,
        decimal maxPercentage,
        decimal defaultPercentage,
        string description)
    {
        if (minPercentage < 0 || maxPercentage > 100)
            throw new ArgumentException("Percentages must be between 0 and 100");

        if (minPercentage > maxPercentage)
            throw new ArgumentException("Min percentage cannot be greater than max percentage");

        if (defaultPercentage < minPercentage || defaultPercentage > maxPercentage)
            throw new ArgumentException("Default percentage must be between min and max");

        return new FeeConfiguration
        {
            Name = name,
            MinPercentage = minPercentage,
            MaxPercentage = maxPercentage,
            DefaultPercentage = defaultPercentage,
            Description = description,
            IsActive = true
        };
    }

    public void UpdatePercentages(decimal minPercentage, decimal maxPercentage, decimal defaultPercentage)
    {
        if (minPercentage < 0 || maxPercentage > 100)
            throw new ArgumentException("Percentages must be between 0 and 100");

        if (minPercentage > maxPercentage)
            throw new ArgumentException("Min percentage cannot be greater than max percentage");

        if (defaultPercentage < minPercentage || defaultPercentage > maxPercentage)
            throw new ArgumentException("Default percentage must be between min and max");

        MinPercentage = minPercentage;
        MaxPercentage = maxPercentage;
        DefaultPercentage = defaultPercentage;
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

    public bool IsValidPercentage(decimal percentage)
    {
        return percentage >= MinPercentage && percentage <= MaxPercentage;
    }
}
