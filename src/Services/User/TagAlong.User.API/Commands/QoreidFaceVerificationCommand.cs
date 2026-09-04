using System.Text;
using System.Text.Json;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Domain.Repositories;
using TagAlong.User.Infrastructure.Services;

namespace TagAlong.User.API.Commands;

public record QoreidFaceVerificationCommand(Guid AuthUserId, string NIN, string PhotoBase64)
    : ICommand<KycStatusResponse>;

public class QoreidFaceVerificationCommandHandler
    : ICommandHandler<QoreidFaceVerificationCommand, KycStatusResponse>
{
    private const string BaseUrl = "https://api.qoreid.com";
    private const double MinConfidence = 70.0;

    private readonly IUserProfileRepository _profiles;
    private readonly IKycVerificationRepository _kycRepo;
    private readonly FileService _fileService;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<QoreidFaceVerificationCommandHandler> _logger;

    public QoreidFaceVerificationCommandHandler(
        IUserProfileRepository profiles,
        IKycVerificationRepository kycRepo,
        FileService fileService,
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<QoreidFaceVerificationCommandHandler> logger)
    {
        _profiles = profiles;
        _kycRepo = kycRepo;
        _fileService = fileService;
        _httpFactory = httpFactory;
        _config = config;
        _logger = logger;
    }

    public async Task<Result<KycStatusResponse>> Handle(
        QoreidFaceVerificationCommand request, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetByAuthUserIdAsync(request.AuthUserId, cancellationToken);
        if (profile == null)
            return Result.Failure<KycStatusResponse>(Error.NotFound("User profile not found"));

        if (profile.IsVerified)
            return Result.Success(new KycStatusResponse(true, "Verified", "Already verified"));

        var apiKey = _config["QoreId:ApiKey"] ?? string.Empty;
        var clientId = _config["QoreId:ClientId"] ?? string.Empty;
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogError("QoreId:ApiKey not configured");
            return Result.Failure<KycStatusResponse>(
                new Error("Error.Config", "Verification service not configured"));
        }

        // Generate a unique reference so the webhook can match back to this user
        var reference = Guid.NewGuid().ToString();

        // Upsert KYC record, storing the reference
        var existing = await _kycRepo.GetByAuthUserIdAsync(request.AuthUserId, cancellationToken);
        KycVerification kyc;
        if (existing != null && existing.Status == KycStatus.Pending)
        {
            kyc = existing;
        }
        else
        {
            kyc = KycVerification.Create(request.AuthUserId, reference);
            await _kycRepo.AddAsync(kyc, cancellationToken);
        }

        // Call QoreID NIN + Face endpoint
        QoreidResponse? qoreResponse = null;
        try
        {
            var http = _httpFactory.CreateClient();
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var payload = JsonSerializer.Serialize(new
            {
                id = request.NIN,
                photo = request.PhotoBase64,
                reference   // QoreID echoes this back in the webhook
            });

            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await http.PostAsync($"{BaseUrl}/v1/ng/identities/nin-face", content, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogInformation("QoreID response for user {UserId}: {Status}", request.AuthUserId, response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("QoreID NIN-face failed for user {UserId}: {Status} {Body}",
                    request.AuthUserId, response.StatusCode, json);

                var errMsg = TryGetErrorMessage(json);
                kyc.Fail($"QoreID error: {response.StatusCode}");
                await _kycRepo.SaveChangesAsync(cancellationToken);
                return Result.Success(new KycStatusResponse(false, "Failed",
                    errMsg ?? "NIN verification failed. Please check your NIN and try again."));
            }

            qoreResponse = JsonSerializer.Deserialize<QoreidResponse>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling QoreID for user {UserId}", request.AuthUserId);
            kyc.Fail("Network error calling QoreID");
            await _kycRepo.SaveChangesAsync(cancellationToken);
            return Result.Failure<KycStatusResponse>(
                new Error("Error.Internal", "Verification service unavailable. Please try again."));
        }

        if (qoreResponse == null)
        {
            kyc.Fail("Empty response from QoreID");
            await _kycRepo.SaveChangesAsync(cancellationToken);
            return Result.Success(new KycStatusResponse(false, "Failed", "NIN not found. Please check your NIN."));
        }

        // Check NIN verification
        var ninStatus = qoreResponse.Summary?.NinCheck?.Status ?? string.Empty;
        if (!ninStatus.Equals("VERIFIED", StringComparison.OrdinalIgnoreCase))
        {
            kyc.Fail($"NIN check not verified: {ninStatus}");
            await _kycRepo.SaveChangesAsync(cancellationToken);
            return Result.Success(new KycStatusResponse(false, "Failed",
                "NIN could not be verified. Please ensure your NIN is correct."));
        }

        // Check face match
        var faceStatus = qoreResponse.Summary?.FaceCheck?.Status ?? string.Empty;
        var confidence = qoreResponse.Summary?.FaceCheck?.Confidence ?? 0.0;

        if (!faceStatus.Equals("MATCH", StringComparison.OrdinalIgnoreCase) || confidence < MinConfidence)
        {
            kyc.Fail($"Face match failed: status={faceStatus} confidence={confidence}");
            await _kycRepo.SaveChangesAsync(cancellationToken);

            var faceMsg = faceStatus.Equals("NO_FACE_FOUND", StringComparison.OrdinalIgnoreCase)
                ? "No face detected in the selfie. Please retake your photo in good lighting."
                : $"Face match failed (confidence: {confidence:F0}%). Please retake your selfie ensuring your face is clearly visible.";

            return Result.Success(new KycStatusResponse(false, "FaceMatchFailed", faceMsg));
        }

        // Save the selfie as the KYC photo
        string? photoPath = null;
        if (!string.IsNullOrEmpty(request.PhotoBase64))
        {
            photoPath = await _fileService.SaveBase64ImageAsync(
                request.PhotoBase64, "kyc", request.AuthUserId.ToString());
        }

        var applicant = qoreResponse.Applicant;
        kyc.Complete(
            nin: request.NIN,
            firstName: applicant?.Firstname,
            lastName: applicant?.Lastname,
            middleName: applicant?.Middlename,
            birthDate: applicant?.Dob,
            gender: applicant?.Gender,
            nationality: "Nigerian",
            residenceState: null,
            photoPath: photoPath);

        await _kycRepo.SaveChangesAsync(cancellationToken);

        profile.Verify(photoPath);
        _profiles.Update(profile);
        await _profiles.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} verified via QoreID. Face confidence: {Confidence}",
            request.AuthUserId, confidence);

        return Result.Success(new KycStatusResponse(true, "Verified", "Identity verified successfully"));
    }

    private static string? TryGetErrorMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("message", out var msg)) return msg.GetString();
            if (root.TryGetProperty("error", out var err)) return err.GetString();
        }
        catch { }
        return null;
    }

    // ── QoreID response models ──

    private class QoreidResponse
    {
        public QoreidApplicant? Applicant { get; set; }
        public QoreidSummary? Summary { get; set; }
        public QoreidStatus? Status { get; set; }
    }

    private class QoreidApplicant
    {
        public string? Firstname { get; set; }
        public string? Lastname { get; set; }
        public string? Middlename { get; set; }
        public string? Dob { get; set; }
        public string? Gender { get; set; }
    }

    private class QoreidSummary
    {
        public QoreidCheck? NinCheck { get; set; }
        public QoreidFaceCheck? FaceCheck { get; set; }
    }

    private class QoreidCheck
    {
        public string? Status { get; set; }
    }

    private class QoreidFaceCheck
    {
        public string? Status { get; set; }
        public double Confidence { get; set; }
    }

    private class QoreidStatus
    {
        public string? State { get; set; }
        public string? Type { get; set; }
    }
}
