namespace TagAlong.User.Domain.Entities;

public class NinCache
{
    public string NIN { get; private set; } = string.Empty;
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string? MiddleName { get; private set; }
    public string? DateOfBirth { get; private set; }
    public string? Gender { get; private set; }
    public DateTime CachedAt { get; private set; }

    private NinCache() { }

    public static NinCache Create(string nin, string? firstName, string? lastName,
        string? middleName, string? dateOfBirth, string? gender) => new()
    {
        NIN = nin,
        FirstName = firstName,
        LastName = lastName,
        MiddleName = middleName,
        DateOfBirth = dateOfBirth,
        Gender = gender,
        CachedAt = DateTime.UtcNow
    };

    public void Refresh(string? firstName, string? lastName, string? middleName,
        string? dateOfBirth, string? gender)
    {
        if (firstName != null) FirstName = firstName;
        if (lastName != null) LastName = lastName;
        if (middleName != null) MiddleName = middleName;
        if (dateOfBirth != null) DateOfBirth = dateOfBirth;
        if (gender != null) Gender = gender;
        CachedAt = DateTime.UtcNow;
    }
}
