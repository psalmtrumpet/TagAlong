using System.Text.Json;
using Anthropic;
using Anthropic.Models.Beta.Messages;
using Microsoft.EntityFrameworkCore;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Infrastructure.Persistence;
using TagAlong.User.Infrastructure.Services;

namespace TagAlong.User.API.Services;

/// <summary>
/// Reads a driver's licence photo and vehicle photo with a Claude vision model,
/// then compares what it read with what the driver typed and with their
/// verified (NIN) name. The result is stored on the driver profile to help the
/// admin review — it never approves or rejects anyone by itself.
/// </summary>
public class DriverDocumentReader
{
    private const string Model = "claude-opus-5-5";

    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger<DriverDocumentReader> _logger;

    public DriverDocumentReader(IServiceScopeFactory scopes, IConfiguration config, ILogger<DriverDocumentReader> logger)
    {
        _scopes = scopes;
        _config = config;
        _logger = logger;
    }

    private string? ApiKey => _config["Anthropic:ApiKey"] is { Length: > 0 } k ? k : null;

    /// <summary>Fire-and-forget: reading takes a few seconds and must not hold up the upload.</summary>
    public void ReadInBackground(Guid authUserId)
    {
        if (ApiKey == null)
        {
            _logger.LogInformation("Document reader: no Anthropic:ApiKey configured — skipping for {UserId}", authUserId);
            return;
        }
        _ = Task.Run(async () =>
        {
            try { await ReadAsync(authUserId, CancellationToken.None); }
            catch (Exception ex) { _logger.LogError(ex, "Document reader failed for {UserId}", authUserId); }
        });
    }

    public async Task ReadAsync(Guid authUserId, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
        var files = scope.ServiceProvider.GetRequiredService<FileService>();

        var driver = await db.DriverProfiles.FirstOrDefaultAsync(d => d.AuthUserId == authUserId, ct);
        if (driver == null) return;

        var licenseFile = files.ResolvePath(driver.LicenseImagePath);
        if (licenseFile == null)
        {
            _logger.LogWarning("Document reader: licence image missing on disk for {UserId}", authUserId);
            return;
        }
        var vehicleFile = files.ResolvePath(driver.VehicleImagePath);

        var extracted = await ExtractAsync(licenseFile, vehicleFile, ct);
        if (extracted == null) return;

        // Names to compare against: verified NIN name first, then profile name
        var kyc = await db.KycVerifications.AsNoTracking()
            .Where(k => k.AuthUserId == authUserId && k.FirstName != null)
            .OrderByDescending(k => k.CompletedAt).FirstOrDefaultAsync(ct);
        var profile = await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.AuthUserId == authUserId, ct);
        var knownName = kyc != null ? $"{kyc.FirstName} {kyc.MiddleName} {kyc.LastName}"
            : profile != null ? $"{profile.FirstName} {profile.LastName}" : null;

        var checks = Compare(driver, extracted.Value, knownName);
        driver.RecordDocumentCheck(JsonSerializer.Serialize(new
        {
            model = Model,
            extracted = extracted.Value,
            checks,
            comparedName = knownName?.Trim(),
        }), checks.Count(c => c.Status == "fail"));
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Document reader: {UserId} checked — {Fails} failed, {Warns} to review",
            authUserId, checks.Count(c => c.Status == "fail"), checks.Count(c => c.Status == "warn"));
    }

    /// <summary>Single vision call returning schema-validated JSON.</summary>
    private async Task<JsonElement?> ExtractAsync(string licenseFile, string? vehicleFile, CancellationToken ct)
    {
        var content = new List<BetaContentBlockParam>
        {
            new BetaTextBlockParam { Text = "Image 1: the driver's licence (front)." },
            Image(licenseFile),
        };
        if (vehicleFile != null)
        {
            content.Add(new BetaTextBlockParam { Text = "Image 2: the driver's vehicle." });
            content.Add(Image(vehicleFile));
        }
        content.Add(new BetaTextBlockParam
        {
            Text = """
                Read these Nigerian ride-sharing driver documents for a human reviewer.
                From the licence: say whether it is actually a driver's licence, and copy the
                licence number, the holder's full name, and the expiry date (YYYY-MM-DD) exactly
                as printed. From the vehicle photo: copy the number plate exactly as printed and
                give the vehicle's colour and make if you can tell.
                Use null for anything you cannot read clearly — never guess. List anything a
                reviewer should look at (blurry, cropped, edited-looking, photo of a screen,
                plate not visible, etc.) in concerns.
                """,
        });

        var client = new AnthropicClient { ApiKey = ApiKey! };
        var response = await client.Beta.Messages.Create(new MessageCreateParams
        {
            Model = Model,
            MaxTokens = 4000,
            // Refusal fallback: a declined request is retried on a fallback model in the same call
            Betas = ["server-side-fallback-2026-06-01"],
            Fallbacks = new List<BetaFallbackParam> { new() { Model = "claude-opus-4-8" } },
            OutputConfig = new BetaOutputConfig { Format = new BetaJsonOutputFormat { Schema = Schema() } },
            Messages = [new BetaMessageParam { Role = Role.User, Content = content }],
        }, ct);

        if (response.StopReason == "refusal")
        {
            _logger.LogWarning("Document reader: model declined to read the documents");
            return null;
        }

        var text = string.Concat(response.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(t => t.Text));
        try
        {
            return JsonDocument.Parse(text).RootElement.Clone();
        }
        catch (JsonException)
        {
            _logger.LogWarning("Document reader: response was not valid JSON");
            return null;
        }
    }

    private static BetaImageBlockParam Image(string path) => new()
    {
        Source = new BetaBase64ImageSource
        {
            Data = Convert.ToBase64String(File.ReadAllBytes(path)),
            MediaType = path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg",
        },
    };

    private static Dictionary<string, JsonElement> Schema()
    {
        object nullableString = new { type = new[] { "string", "null" } };
        return new Dictionary<string, JsonElement>
        {
            ["type"] = JsonSerializer.SerializeToElement("object"),
            ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
            ["required"] = JsonSerializer.SerializeToElement(new[] { "license", "vehicle", "concerns" }),
            ["properties"] = JsonSerializer.SerializeToElement(new Dictionary<string, object>
            {
                ["license"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new[] { "isDriversLicence", "licenceNumber", "holderName", "expiryDate" },
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["isDriversLicence"] = new { type = "boolean" },
                        ["licenceNumber"] = nullableString,
                        ["holderName"] = nullableString,
                        ["expiryDate"] = nullableString,
                    },
                },
                ["vehicle"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new[] { "plateNumber", "colour", "make" },
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["plateNumber"] = nullableString,
                        ["colour"] = nullableString,
                        ["make"] = nullableString,
                    },
                },
                ["concerns"] = new { type = "array", items = new { type = "string" } },
            }),
        };
    }

    public record Check(string Field, string Status, string Detail);

    /// <summary>Plain comparisons — pass / warn (needs a look) / fail (mismatch).</summary>
    private static List<Check> Compare(DriverProfile d, JsonElement x, string? knownName)
    {
        var checks = new List<Check>();
        var lic = x.GetProperty("license");
        var veh = x.GetProperty("vehicle");

        if (!lic.GetProperty("isDriversLicence").GetBoolean())
            checks.Add(new("Licence", "fail", "The licence photo doesn't look like a driver's licence."));

        var readNumber = Str(lic, "licenceNumber");
        checks.Add(readNumber == null
            ? new("Licence number", "warn", "Couldn't read the licence number from the photo.")
            : Alnum(readNumber) == Alnum(d.LicenseNumber)
                ? new("Licence number", "pass", $"Matches what the driver entered ({readNumber}).")
                : new("Licence number", "fail", $"Photo shows {readNumber}, driver entered {d.LicenseNumber}."));

        var readName = Str(lic, "holderName");
        if (readName == null)
            checks.Add(new("Name", "warn", "Couldn't read the holder's name."));
        else if (knownName == null)
            checks.Add(new("Name", "warn", $"Licence name is {readName}; no verified name to compare with."));
        else
        {
            var overlap = Words(readName).Intersect(Words(knownName)).Count();
            checks.Add(overlap >= 2
                ? new("Name", "pass", $"Licence name {readName} matches the verified name.")
                : new("Name", overlap == 1 ? "warn" : "fail", $"Licence name {readName} vs verified name {knownName.Trim()}."));
        }

        var expiry = Str(lic, "expiryDate");
        if (expiry == null)
            checks.Add(new("Expiry", "warn", "Couldn't read the expiry date."));
        else if (DateTime.TryParse(expiry, out var exp))
            checks.Add(exp.Date < DateTime.UtcNow.Date
                ? new("Expiry", "fail", $"Licence expired on {exp:dd MMM yyyy}.")
                : new("Expiry", "pass", $"Valid until {exp:dd MMM yyyy}."));

        if (d.VehicleImagePath != null)
        {
            var plate = Str(veh, "plateNumber");
            checks.Add(plate == null
                ? new("Plate", "warn", "Couldn't read the plate in the vehicle photo.")
                : Alnum(plate) == Alnum(d.VehiclePlate)
                    ? new("Plate", "pass", $"Matches what the driver entered ({plate}).")
                    : new("Plate", "fail", $"Photo shows {plate}, driver entered {d.VehiclePlate}."));

            var colour = Str(veh, "colour");
            if (colour != null && !Words(colour).Intersect(Words(d.VehicleColor)).Any())
                checks.Add(new("Colour", "warn", $"Photo looks {colour}, driver entered {d.VehicleColor}."));
        }
        else
        {
            checks.Add(new("Plate", "warn", "No vehicle photo was uploaded."));
        }

        foreach (var c in x.GetProperty("concerns").EnumerateArray())
            if (c.GetString() is { Length: > 0 } s) checks.Add(new("Photo", "warn", s));

        return checks;
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()!.Trim() : null;

    private static string Alnum(string s) => new(s.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static HashSet<string> Words(string s) =>
        s.ToUpperInvariant().Split(new[] { ' ', ',', '.', '-' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 1).ToHashSet();
}
