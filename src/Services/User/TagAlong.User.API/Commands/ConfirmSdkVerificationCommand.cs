using System.Text.Json;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Domain.Repositories;
using TagAlong.User.Infrastructure.Services;

namespace TagAlong.User.API.Commands;

public record ConfirmSdkVerificationCommand(Guid AuthUserId, string ReferenceId) : ICommand<KycStatusResponse>;

public class ConfirmSdkVerificationCommandHandler : ICommandHandler<ConfirmSdkVerificationCommand, KycStatusResponse>
{
    private readonly IUserProfileRepository _profiles;
    private readonly IKycVerificationRepository _kycRepo;
    private readonly FileService _fileService;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<ConfirmSdkVerificationCommandHandler> _logger;

    public ConfirmSdkVerificationCommandHandler(
        IUserProfileRepository profiles,
        IKycVerificationRepository kycRepo,
        FileService fileService,
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<ConfirmSdkVerificationCommandHandler> logger)
    {
        _profiles = profiles;
        _kycRepo = kycRepo;
        _fileService = fileService;
        _httpFactory = httpFactory;
        _config = config;
        _logger = logger;
    }

    public async Task<Result<KycStatusResponse>> Handle(ConfirmSdkVerificationCommand request, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetByAuthUserIdAsync(request.AuthUserId, cancellationToken);
        if (profile == null)
            return Result.Failure<KycStatusResponse>(Error.NotFound("User profile not found"));

        if (profile.IsVerified)
            return Result.Success(new KycStatusResponse(true, "Verified", "Already verified"));

        var appId = _config["Dojah:AppId"] ?? string.Empty;
        var secretKey = _config["Dojah:PrivateKey"] ?? string.Empty;

        var http = _httpFactory.CreateClient();
        http.DefaultRequestHeaders.Add("AppId", appId);
        http.DefaultRequestHeaders.Add("Authorization", secretKey);

        NinEntity? ninEntity = null;
        string? imageUrl = null;

        try
        {
            var url = $"https://api.dojah.io/api/v1/kyc/verification/{Uri.EscapeDataString(request.ReferenceId)}";
            var response = await http.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Dojah verification fetch failed: {Status} {Body}", response.StatusCode, json);
                return Result.Failure<KycStatusResponse>(new Error("Error.Internal", "Could not confirm verification. Please try again."));
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Navigate: data.government_data.data.nin.entity
            if (root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("government_data", out var govData) &&
                govData.TryGetProperty("data", out var govDataInner) &&
                govDataInner.TryGetProperty("nin", out var ninEl) &&
                ninEl.TryGetProperty("entity", out var entity))
            {
                ninEntity = new NinEntity(
                    Nin: GetString(entity, "nin"),
                    FirstName: GetString(entity, "firstname"),
                    MiddleName: GetString(entity, "middlename"),
                    Surname: GetString(entity, "surname"),
                    BirthDate: GetString(entity, "birthdate"),
                    Gender: GetString(entity, "gender"),
                    Nationality: GetString(entity, "nationality"),
                    ResidenceState: GetString(entity, "residence_state"),
                    ImageUrl: GetString(entity, "image_url")  // expiring URL from widget response
                );
                imageUrl = ninEntity.ImageUrl;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Dojah verification for ref {Ref}", request.ReferenceId);
            return Result.Failure<KycStatusResponse>(new Error("Error.Internal", "Verification service error. Please try again."));
        }

        // Download and save the NIN photo (URL expires in 1 hour)
        string? photoPath = null;
        if (!string.IsNullOrEmpty(imageUrl))
        {
            var httpDownload = _httpFactory.CreateClient();
            photoPath = await _fileService.SaveFromUrlAsync(httpDownload, imageUrl, "kyc", request.AuthUserId.ToString());
        }

        var kyc = KycVerification.Create(request.AuthUserId);
        kyc.Complete(
            ninEntity?.Nin ?? string.Empty,
            ninEntity?.FirstName,
            ninEntity?.Surname,
            ninEntity?.MiddleName,
            ninEntity?.BirthDate,
            ninEntity?.Gender,
            ninEntity?.Nationality,
            ninEntity?.ResidenceState,
            photoPath);

        await _kycRepo.AddAsync(kyc, cancellationToken);

        profile.Verify(photoPath);
        _profiles.Update(profile);

        await _kycRepo.SaveChangesAsync(cancellationToken);

        return Result.Success(new KycStatusResponse(true, "Verified", "Identity verified successfully"));
    }

    private static string? GetString(JsonElement el, string key)
        => el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private record NinEntity(
        string? Nin, string? FirstName, string? MiddleName, string? Surname,
        string? BirthDate, string? Gender, string? Nationality, string? ResidenceState, string? ImageUrl);
}
