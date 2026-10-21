using System.Text.RegularExpressions;

namespace TagAlong.Identity.API.Validators;

/// <summary>What a new password must contain (sign-up, change and reset).</summary>
public static class PasswordRules
{
    /// <summary>The first rule the password breaks, or null if it's fine.</summary>
    public static string? Problem(string? password)
    {
        if (string.IsNullOrEmpty(password)) return "Password is required";
        if (password.Length < 8) return "Password must be at least 8 characters";
        if (!Regex.IsMatch(password, "[A-Z]")) return "Password must contain at least one uppercase letter";
        if (!Regex.IsMatch(password, "[a-z]")) return "Password must contain at least one lowercase letter";
        if (!Regex.IsMatch(password, "[0-9]")) return "Password must contain at least one number";
        if (!Regex.IsMatch(password, "[^A-Za-z0-9\\s]")) return "Password must contain at least one special character (e.g. ! @ # $ - _)";
        return null;
    }
}
