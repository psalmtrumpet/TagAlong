using System.Net.Http.Headers;
using System.Text.Json;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Domain.Repositories;
using TagAlong.User.Infrastructure.Services;

namespace TagAlong.User.API.Commands;

public record VerifyNinCommand(Guid AuthUserId, string NIN) : ICommand<KycStatusResponse>;

public record KycStatusResponse(bool IsVerified, string Status, string? Message);

public class VerifyNinCommandHandler : ICommandHandler<VerifyNinCommand, KycStatusResponse>
{
    private readonly IUserProfileRepository _profiles;
    private readonly IKycVerificationRepository _kycRepo;
    private readonly FileService _fileService;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<VerifyNinCommandHandler> _logger;

    public VerifyNinCommandHandler(
        IUserProfileRepository profiles,
        IKycVerificationRepository kycRepo,
        FileService fileService,
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<VerifyNinCommandHandler> logger)
    {
        _profiles = profiles;
        _kycRepo = kycRepo;
        _fileService = fileService;
        _httpFactory = httpFactory;
        _config = config;
        _logger = logger;
    }

    public async Task<Result<KycStatusResponse>> Handle(VerifyNinCommand request, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetByAuthUserIdAsync(request.AuthUserId, cancellationToken);
        if (profile == null)
            return Result.Failure<KycStatusResponse>(Error.NotFound("User profile not found"));

        if (profile.IsVerified)
            return Result.Success(new KycStatusResponse(true, "Verified", "Already verified"));

        // Create a pending KYC record (replace any existing pending one)
        var existing = await _kycRepo.GetByAuthUserIdAsync(request.AuthUserId, cancellationToken);
        KycVerification kyc;
        if (existing != null && existing.Status == KycStatus.Pending)
        {
            kyc = existing;
        }
        else
        {
            kyc = KycVerification.Create(request.AuthUserId);
            await _kycRepo.AddAsync(kyc, cancellationToken);
        }

        // Call Dojah NIN API
        var appId = _config["Dojah:AppId"] ?? string.Empty;
        var secretKey = _config["Dojah:PrivateKey"] ?? string.Empty;

        var http = _httpFactory.CreateClient();
        http.DefaultRequestHeaders.Add("AppId", appId);
        http.DefaultRequestHeaders.Add("Authorization", secretKey);

        DojahNinEntity? entity = null;
        try
        {
            var url = $"https://api.dojah.io/api/v1/kyc/nin?nin={Uri.EscapeDataString(request.NIN)}";
            var response = await http.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Dojah NIN lookup failed for user {UserId}: {Status} {Body}", request.AuthUserId, response.StatusCode, json);
                kyc.Fail($"Dojah error: {response.StatusCode}");
                _kycRepo.Update(kyc);
                await _kycRepo.SaveChangesAsync(cancellationToken);
                return Result.Success(new KycStatusResponse(false, "Failed", "NIN verification failed. Please check your NIN and try again."));
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            entity = ParseNinEntity(root);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Dojah NIN API for user {UserId}", request.AuthUserId);
            kyc.Fail("Network error calling verification service");
            _kycRepo.Update(kyc);
            await _kycRepo.SaveChangesAsync(cancellationToken);
            return Result.Failure<KycStatusResponse>(new Error("Error.Internal", "Verification service unavailable. Please try again."));
        }

        if (entity == null)
        {
            kyc.Fail("No data returned from Dojah");
            _kycRepo.Update(kyc);
            await _kycRepo.SaveChangesAsync(cancellationToken);
            return Result.Success(new KycStatusResponse(false, "Failed", "NIN not found. Please check your NIN."));
        }

        // Save NIN photo — Dojah direct API returns base64 in entity.photo
        string? photoPath = null;
        if (!string.IsNullOrEmpty(entity.Photo))
        {
            photoPath = await _fileService.SaveBase64ImageAsync(entity.Photo, "kyc", request.AuthUserId.ToString());
        }

        kyc.Complete(
            entity.Nin ?? request.NIN,
            entity.FirstName,
            entity.Surname,
            entity.MiddleName,
            entity.BirthDate,
            entity.Gender,
            entity.Nationality,
            entity.ResidenceState,
            photoPath);

        _kycRepo.Update(kyc);

        await _kycRepo.SaveChangesAsync(cancellationToken);

        // NIN is valid — frontend will now launch liveness check before marking verified
        return Result.Success(new KycStatusResponse(true, "NinValidated", "NIN verified. Please complete the face check."));
    }

    private static DojahNinEntity? ParseNinEntity(JsonElement root)
    {
        try
        {
            if (root.TryGetProperty("entity", out var entityEl))
            {
                return new DojahNinEntity(
                    Nin: GetString(entityEl, "nin"),
                    FirstName: GetString(entityEl, "firstname"),
                    MiddleName: GetString(entityEl, "middlename"),
                    Surname: GetString(entityEl, "surname"),
                    BirthDate: GetString(entityEl, "birthdate"),
                    Gender: GetString(entityEl, "gender"),
                    Nationality: GetString(entityEl, "nationality"),
                    ResidenceState: GetString(entityEl, "residence_state"),
                    Photo: GetString(entityEl, "photo")
                );
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetString(JsonElement el, string key)
        => el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private record DojahNinEntity(
        string? Nin, string? FirstName, string? MiddleName, string? Surname,
        string? BirthDate, string? Gender, string? Nationality, string? ResidenceState, string? Photo);
}
