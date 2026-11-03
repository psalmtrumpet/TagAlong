using TagAlong.Common.Domain;

namespace TagAlong.Identity.Domain.Entities;

public class VerificationCode : Entity
{
    public Guid UserId { get; private set; }
    public string Code { get; private set; } = null!;
    public VerificationType Type { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public bool IsUsed { get; private set; }
    public DateTime? UsedAt { get; private set; }

    private VerificationCode() { }

    public static VerificationCode Create(Guid userId, VerificationType type, int expirationMinutes = 15)
    {
        return new VerificationCode
        {
            UserId = userId,
            Code = GenerateCode(),
            Type = type,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes)
        };
    }

    public bool IsValid() => !IsUsed && DateTime.UtcNow < ExpiresAt;

    public void MarkAsUsed()
    {
        IsUsed = true;
        UsedAt = DateTime.UtcNow;
        SetUpdated();
    }

    private static string GenerateCode()
    {
        return System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
    }
}

public enum VerificationType
{
    Email,
    Phone,
    PasswordReset
}
