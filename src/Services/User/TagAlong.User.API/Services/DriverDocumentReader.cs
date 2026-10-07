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
/// admin review. A fresh submission is scored 0–100 from those checks:
/// 90+ with every key check passing is approved automatically, below 60 is
/// rejected automatically (the driver is told what to fix), and anything in
/// between waits for an admin.
/// </summary>
public class DriverDocumentReader
{
    private const string Model = "claude-opus-5-5";
    private const int ApproveAt = 90;
    private const int RejectBelow = 60;
    // camelCase like the rest of the API, so the admin portal can read it
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly DriverReviewNotifier _notifier;
    private readonly ILogger<DriverDocumentReader> _logger;

    public DriverDocumentReader(IServiceScopeFactory scopes, IConfiguration config, DriverReviewNotifier notifier, ILogger<DriverDocumentReader> logger)
    {
        _scopes = scopes;
        _config = config;
        _notifier = notifier;
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
            try { await ReadAsync(authUserId, CancellationToken.None, autoDecide: true); }
            catch (Exception ex) { _logger.LogError(ex, "Document reader failed for {UserId}", authUserId); }
        });
    }

    /// <param name="autoDecide">Approve or reject a pending submission from its score (new uploads only).</param>
    public async Task ReadAsync(Guid authUserId, CancellationToken ct, bool autoDecide = false)
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
        var frontFile = files.ResolvePath(driver.VehicleImagePath);
        var backFile = files.ResolvePath(driver.VehicleBackImagePath);

        var extracted = await ExtractAsync(licenseFile, frontFile, backFile, ct);
        if (extracted == null) return;

        // Names to compare against: verified NIN name first, then profile name
        var kyc = await db.KycVerifications.AsNoTracking()
            .Where(k => k.AuthUserId == authUserId && k.FirstName != null)
            .OrderByDescending(k => k.CompletedAt).FirstOrDefaultAsync(ct);
        var profile = await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.AuthUserId == authUserId, ct);
        var knownName = kyc != null ? $"{kyc.FirstName} {kyc.MiddleName} {kyc.LastName}"
            : profile != null ? $"{profile.FirstName} {profile.LastName}" : null;

        var checks = Compare(driver, extracted.Value, knownName);
        var fails = checks.Count(c => c.Status == "fail");
        var score = Score(extracted.Value, checks);

        // 90+ and every key check passing → approve; under 60 → reject; else an admin decides
        var decision = "review";
        var problems = new List<string>();
        if (autoDecide && driver.Status == DriverProfileStatus.Pending)
        {
            if (score >= ApproveAt && AllKeyChecksPass(extracted.Value, checks))
            {
                decision = "approved";
                driver.ApproveAutomatically();
            }
            else if (score < RejectBelow)
            {
                decision = "rejected";
                problems = DriverProblems(extracted.Value, checks);
                driver.RejectAutomatically(string.Join(" ", problems));
            }
        }

        driver.RecordDocumentCheck(JsonSerializer.Serialize(new
        {
            model = Model,
            score,
            decision,
            extracted = extracted.Value,
            checks,
            comparedName = knownName?.Trim(),
        }, JsonOptions), fails);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Document reader: {UserId} scored {Score} ({Decision}) — {Fails} failed, {Warns} to review",
            authUserId, score, decision, fails, checks.Count(c => c.Status == "warn"));

        if (decision == "approved")
            await _notifier.NotifyAsync(authUserId, approved: true, Array.Empty<string>(), ct);
        else if (decision == "rejected")
            await _notifier.NotifyAsync(authUserId, approved: false, problems, ct);
    }

    /// <summary>Single vision call returning schema-validated JSON.</summary>
    private async Task<JsonElement?> ExtractAsync(string licenseFile, string? frontFile, string? backFile, CancellationToken ct)
    {
        var content = new List<BetaContentBlockParam>
        {
            new BetaTextBlockParam { Text = "Image 1: the driver's licence (front)." },
            Image(licenseFile),
        };
        if (frontFile != null)
        {
            content.Add(new BetaTextBlockParam { Text = "Vehicle photo: the FRONT of the driver's vehicle." });
            content.Add(Image(frontFile));
        }
        if (backFile != null)
        {
            content.Add(new BetaTextBlockParam { Text = "Vehicle photo: the BACK of the driver's vehicle." });
            content.Add(Image(backFile));
        }
        content.Add(new BetaTextBlockParam
        {
            Text = """
                Read these Nigerian ride-sharing driver documents for a human reviewer.
                From the licence: say whether it is actually a driver's licence, and copy the
                licence number, the holder's full name, and the expiry date (YYYY-MM-DD) exactly
                as printed. From the vehicle photos: copy the number plate on the front and the
                number plate on the back exactly as printed, give the vehicle's colour and make
                if you can tell, and say whether the front and back photos show the same vehicle
                (null if you can't tell or a photo is missing).
                Use null for anything you cannot read clearly — never guess. List anything a
                reviewer should look at (blurry, cropped, edited-looking, photo of a screen,
                plate not visible, etc.) in concerns.
                In photosUsable, say for each photo whether it is acceptable: true only if it is a
                real, sharp photo of the right thing (the licence, or the front / back of a vehicle
                with its plate readable) — false if it is blurry, has glare over key details, is a
                screenshot or photo of a screen, or shows something else. Use null for a photo that
                wasn't provided.
                In driverFixes, write one short, polite sentence addressed to the driver for each
                thing they must fix (e.g. "Your licence photo is blurry — retake it in good light so
                the licence number can be read."). Leave it empty if nothing needs fixing.
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
            ["required"] = JsonSerializer.SerializeToElement(new[] { "license", "vehicle", "concerns", "photosUsable", "driverFixes" }),
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
                    ["required"] = new[] { "frontPlate", "backPlate", "colour", "make", "sameVehicle" },
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["frontPlate"] = nullableString,
                        ["backPlate"] = nullableString,
                        ["colour"] = nullableString,
                        ["make"] = nullableString,
                        ["sameVehicle"] = new { type = new[] { "boolean", "null" } },
                    },
                },
                ["concerns"] = new { type = "array", items = new { type = "string" } },
                ["photosUsable"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new[] { "licence", "vehicleFront", "vehicleBack" },
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["licence"] = new { type = new[] { "boolean", "null" } },
                        ["vehicleFront"] = new { type = new[] { "boolean", "null" } },
                        ["vehicleBack"] = new { type = new[] { "boolean", "null" } },
                    },
                },
                ["driverFixes"] = new { type = "array", items = new { type = "string" } },
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

        PlateCheck("Front plate", d.VehicleImagePath != null, Str(veh, "frontPlate"));
        PlateCheck("Back plate", d.VehicleBackImagePath != null, Str(veh, "backPlate"));

        var front = Str(veh, "frontPlate");
        var back = Str(veh, "backPlate");
        if (front != null && back != null && Alnum(front) != Alnum(back))
            checks.Add(new("Plates", "fail", $"Front plate {front} and back plate {back} are different."));

        if (veh.TryGetProperty("sameVehicle", out var same) && same.ValueKind == JsonValueKind.False)
            checks.Add(new("Vehicle", "fail", "The front and back photos look like different vehicles."));

        if (d.VehicleImagePath != null || d.VehicleBackImagePath != null)
        {
            var colour = Str(veh, "colour");
            if (colour != null && !Words(colour).Intersect(Words(d.VehicleColor)).Any())
                checks.Add(new("Colour", "warn", $"Photo looks {colour}, driver entered {d.VehicleColor}."));
        }

        void PlateCheck(string field, bool uploaded, string? read)
        {
            if (!uploaded)
                checks.Add(new(field, "warn", $"No {field.ToLowerInvariant().Replace(" plate", "")} vehicle photo was uploaded."));
            else if (read == null)
                checks.Add(new(field, "warn", $"Couldn't read the plate in the {field.ToLowerInvariant().Replace(" plate", "")} photo."));
            else if (Alnum(read) == Alnum(d.VehiclePlate))
                checks.Add(new(field, "pass", $"Matches what the driver entered ({read})."));
            else
                checks.Add(new(field, "fail", $"Photo shows {read}, driver entered {d.VehiclePlate}."));
        }

        foreach (var c in x.GetProperty("concerns").EnumerateArray())
            if (c.GetString() is { Length: > 0 } s) checks.Add(new("Photo", "warn", s));

        return checks;
    }

    /// <summary>
    /// 0–100 from the checks: mismatches and unusable photos cost the most,
    /// unreadable details less, and reviewer notes a little each.
    /// </summary>
    private static int Score(JsonElement x, List<Check> checks)
    {
        var score = 100;
        var notes = 0;
        foreach (var c in checks)
        {
            score -= (c.Field, c.Status) switch
            {
                ("Licence", "fail") => 60,
                ("Expiry", "fail") => 60,
                ("Vehicle", "fail") => 50,
                ("Licence number", "fail") => 40,
                ("Name", "fail") => 40,
                ("Front plate", "fail") or ("Back plate", "fail") or ("Plates", "fail") => 35,
                ("Front plate", "warn") or ("Back plate", "warn") => 15,
                (_, "warn") when c.Field != "Photo" && c.Field != "Colour" => 10,
                ("Colour", "warn") => 5,
                _ => 0,
            };
            if (c.Field == "Photo") notes++;
        }
        score -= Math.Min(notes * 3, 15);

        if (x.TryGetProperty("photosUsable", out var u))
        {
            if (Is(u, "licence", JsonValueKind.False)) score -= 20;
            if (Is(u, "vehicleFront", JsonValueKind.False)) score -= 20;
            if (Is(u, "vehicleBack", JsonValueKind.False)) score -= 20;
        }
        return Math.Clamp(score, 0, 100);
    }

    /// <summary>Approve only when every photo is usable and every key detail was read and matches.</summary>
    private static bool AllKeyChecksPass(JsonElement x, List<Check> checks)
    {
        if (checks.Any(c => c.Status == "fail")) return false;
        if (!x.TryGetProperty("photosUsable", out var u)
            || !Is(u, "licence", JsonValueKind.True) || !Is(u, "vehicleFront", JsonValueKind.True) || !Is(u, "vehicleBack", JsonValueKind.True))
            return false;
        string[] mustPass = { "Licence number", "Name", "Expiry", "Front plate", "Back plate" };
        return mustPass.All(f => checks.Any(c => c.Field == f && c.Status == "pass"));
    }

    private static bool Is(JsonElement e, string name, JsonValueKind kind) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == kind;

    /// <summary>What the driver must fix, for the rejection email and the app.</summary>
    private static List<string> DriverProblems(JsonElement x, List<Check> checks)
    {
        var unusable = new List<string>();
        if (x.TryGetProperty("photosUsable", out var u))
        {
            if (u.TryGetProperty("licence", out var l) && l.ValueKind == JsonValueKind.False)
                unusable.Add("Your licence photo isn't a clear photo of your driver's licence.");
            if (u.TryGetProperty("vehicleFront", out var f) && f.ValueKind == JsonValueKind.False)
                unusable.Add("Your front vehicle photo doesn't clearly show the front of your vehicle and its plate number.");
            if (u.TryGetProperty("vehicleBack", out var b) && b.ValueKind == JsonValueKind.False)
                unusable.Add("Your back vehicle photo doesn't clearly show the back of your vehicle and its plate number.");
        }
        var mismatches = checks.Where(c => c.Status == "fail").Select(c => $"{c.Field}: {c.Detail}").ToList();

        // Prefer the reader's own wording for the driver; fall back to our own lines
        var fixes = x.TryGetProperty("driverFixes", out var df) && df.ValueKind == JsonValueKind.Array
            ? df.EnumerateArray().Select(e => e.GetString()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()).ToList()
            : new List<string>();
        if (fixes.Count > 0) return fixes;
        var ours = unusable.Concat(mismatches).ToList();
        return ours.Count > 0
            ? ours
            : checks.Where(c => c.Status == "warn" && c.Field != "Photo").Select(c => c.Detail).ToList();
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()!.Trim() : null;

    private static string Alnum(string s) => new(s.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static HashSet<string> Words(string s) =>
        s.ToUpperInvariant().Split(new[] { ' ', ',', '.', '-' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 1).ToHashSet();
}
