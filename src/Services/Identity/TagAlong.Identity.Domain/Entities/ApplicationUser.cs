using TagAlong.Common.Domain;

namespace TagAlong.Identity.Domain.Entities;

public class ApplicationUser : AggregateRoot
{
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string? GoogleId { get; private set; }
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public string PhoneNumber { get; private set; } = null!;
    public bool IsEmailVerified { get; private set; }
    public bool IsPhoneVerified { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime? RefreshTokenExpiryTime { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string? ProfileImageUrl { get; private set; }

    private ApplicationUser() { }

    public static ApplicationUser Create(
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        string phoneNumber)
    {
        var user = new ApplicationUser
        {
            Email = email.ToLowerInvariant(),
            PasswordHash = passwordHash,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phoneNumber
        };

        return user;
    }

    public static ApplicationUser CreateFromGoogle(
        string email,
        string googleId,
        string firstName,
        string lastName,
        string? profileImageUrl = null)
    {
        var user = new ApplicationUser
        {
            Email = email.ToLowerInvariant(),
            GoogleId = googleId,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = string.Empty,
            IsEmailVerified = true,
            ProfileImageUrl = profileImageUrl,
            PasswordHash = string.Empty
        };

        return user;
    }

    public void UpdateRefreshToken(string refreshToken, DateTime expiryTime)
    {
        RefreshToken = refreshToken;
        RefreshTokenExpiryTime = expiryTime;
        SetUpdated();
    }

    public void RevokeRefreshToken()
    {
        RefreshToken = null;
        RefreshTokenExpiryTime = null;
        SetUpdated();
    }

    public void UpdateLastLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void VerifyEmail()
    {
        IsEmailVerified = true;
        SetUpdated();
    }

    public void VerifyPhone()
    {
        IsPhoneVerified = true;
        SetUpdated();
    }

    public void UpdatePhoneNumber(string phoneNumber)
    {
        PhoneNumber = phoneNumber;
        IsPhoneVerified = false;
        SetUpdated();
    }

    public void UpdatePassword(string passwordHash)
    {
        PasswordHash = passwordHash;
        SetUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        SetUpdated();
    }

    public void LinkGoogleAccount(string googleId)
    {
        GoogleId = googleId;
        SetUpdated();
    }
}
