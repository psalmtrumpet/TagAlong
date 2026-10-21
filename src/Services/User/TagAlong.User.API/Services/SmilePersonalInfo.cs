using System.Text.RegularExpressions;

namespace TagAlong.User.API.Services;

/// <summary>
/// The person's details from Smile ID's ID-data callback (result 1012): name,
/// date of birth and gender as held against their NIN. Smile sends these in a
/// separate callback from the final "verified" one.
/// </summary>
public record SmilePersonalInfo(string? FirstName, string? LastName, string? MiddleName, string? DateOfBirth, string? Gender)
{
    public bool HasAny => FirstName != null || LastName != null || DateOfBirth != null;

    /// <summary>Fills in last/middle name from the full name when Smile only sends some parts.</summary>
    public static SmilePersonalInfo From(string? first, string? last, string? middle, string? full, string? dob, string? gender)
    {
        first = Clean(first); last = Clean(last); middle = Clean(middle); full = Clean(full);
        if (full != null && (last == null || first == null))
        {
            var parts = full.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (first != null && parts.Count >= 2 && string.Equals(parts[0], first, StringComparison.OrdinalIgnoreCase))
            {
                // FIRST MIDDLE… LAST
                last ??= parts[^1];
                middle ??= parts.Count > 2 ? string.Join(' ', parts.Skip(1).Take(parts.Count - 2)) : null;
            }
            else if (parts.Count >= 2)
            {
                // NIN order: SURNAME FIRST MIDDLE…
                last ??= parts[0];
                first ??= parts[1];
                middle ??= parts.Count > 2 ? string.Join(' ', parts.Skip(2).Where(p => !string.Equals(p, first, StringComparison.OrdinalIgnoreCase))) : null;
            }
            else if (parts.Count == 1)
            {
                first ??= parts[0];
            }
        }
        return new SmilePersonalInfo(Title(first), Title(last), Title(middle), Clean(dob), NormalizeGender(gender));
    }

    /// <summary>Reads the details out of a stored callback body (it may be cut short).</summary>
    public static SmilePersonalInfo FromBody(string body) => From(
        Field(body, "FirstName"), Field(body, "LastName"), Field(body, "MiddleName"),
        Field(body, "FullName"), Field(body, "DOB"), Field(body, "Gender"));

    /// <summary>The address held against the NIN, from a callback body.</summary>
    public static string? AddressFrom(string body) => Title(Clean(Field(body, "Address")));

    private static string? Field(string body, string name)
    {
        var m = Regex.Match(body, $"\"{name}\"\\s*:\\s*\"([^\"]*)\"");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string? Clean(string? s) =>
        string.IsNullOrWhiteSpace(s) || s.Trim().Equals("Not Available", StringComparison.OrdinalIgnoreCase) ? null : s.Trim();

    private static string? Title(string? s) =>
        s == null ? null : string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpper(w[0]) + w[1..].ToLower()));

    private static string? NormalizeGender(string? g) => Clean(g)?.ToLower() switch
    {
        "m" or "male" => "Male",
        "f" or "female" => "Female",
        var other => other,
    };
}
