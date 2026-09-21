namespace TagAlong.User.API.Services;

public static class NinNameMatcher
{
    /// <summary>
    /// Returns true if any one of the four cross-comparisons matches:
    /// profileFirst==smileFirst, profileLast==smileLast,
    /// profileFirst==smileLast, profileLast==smileFirst.
    /// Comparison is case-insensitive and trims whitespace.
    /// </summary>
    public static bool NamesMatch(string? profileFirst, string? profileLast,
        string? smileFirst, string? smileLast)
    {
        if (string.IsNullOrWhiteSpace(smileFirst) && string.IsNullOrWhiteSpace(smileLast))
            return false;

        var pf = N(profileFirst);
        var pl = N(profileLast);
        var sf = N(smileFirst);
        var sl = N(smileLast);

        return (pf.Length > 0 && sf.Length > 0 && pf == sf)
            || (pl.Length > 0 && sl.Length > 0 && pl == sl)
            || (pf.Length > 0 && sl.Length > 0 && pf == sl)
            || (pl.Length > 0 && sf.Length > 0 && pl == sf);
    }

    public static string BuildMismatchReason(string profileFirst, string profileLast,
        string? smileFirst, string? smileLast)
        => $"The name on your NIN ({smileFirst} {smileLast}) does not match your account name " +
           $"({profileFirst} {profileLast}). Please ensure the name on your TagAlong account " +
           "matches exactly what is on your National Identity Card.";

    public static string BuildFailureEmailHtml(string firstName, string reason)
        => $"""
            <div style="font-family:sans-serif;max-width:520px;margin:0 auto;padding:24px">
              <h2 style="color:#e53e3e">Identity Verification Failed</h2>
              <p>Hi {firstName},</p>
              <p>We were unable to verify your identity on TagAlong.</p>
              <p style="background:#fff5f5;border-left:4px solid #e53e3e;padding:12px;border-radius:4px">
                {reason}
              </p>
              <p>To fix this, please go to <strong>Settings → Edit Profile</strong> and make sure
              your first and last name match exactly what appears on your National ID card.</p>
              <p>If you believe this is an error, please contact our support team.</p>
              <p>— The TagAlong Team</p>
            </div>
            """;

    private static string N(string? s) => s?.Trim().ToLowerInvariant() ?? string.Empty;
}
