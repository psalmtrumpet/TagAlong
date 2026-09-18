using System.Text;
using System.Text.Json;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Domain.Repositories;
using TagAlong.User.Infrastructure.Services;

namespace TagAlong.User.API.Commands;

public record QoreidFaceVerificationCommand(Guid AuthUserId, string BVN, string PhotoBase64)
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

        var clientId = _config["QoreId:ClientId"] ?? string.Empty;
        var secret   = _config["QoreId:Secret"]   ?? string.Empty;
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(secret))
        {
            _logger.LogError("QoreId:ClientId or QoreId:Secret not configured");
            return Result.Failure<KycStatusResponse>(
                new Error("Error.Config", "Verification service not configured"));
        }

        // Upsert KYC record
        var existing = await _kycRepo.GetByAuthUserIdAsync(request.AuthUserId, cancellationToken);
        KycVerification kyc;
        if (existing != null && existing.Status == KycStatus.Pending)
        {
            kyc = existing;
        }
        else
        {
            kyc = KycVerification.Create(request.AuthUserId, Guid.NewGuid().ToString());
            await _kycRepo.AddAsync(kyc, cancellationToken);
        }

        // Step 1: get a short-lived access token from QoreID
        string accessToken;
        try
        {
            var http = _httpFactory.CreateClient();
            var tokenPayload = JsonSerializer.Serialize(new { clientId, secret });
            var tokenContent = new StringContent(tokenPayload, Encoding.UTF8, "application/json");
            var tokenResponse = await http.PostAsync($"{BaseUrl}/token", tokenContent, cancellationToken);
            var tokenJson = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!tokenResponse.IsSuccessStatusCode)
            {
                _logger.LogError("QoreID token exchange failed: {Status} {Body}", tokenResponse.StatusCode, tokenJson);
                kyc.Fail("QoreID auth failed");
                await _kycRepo.SaveChangesAsync(cancellationToken);
                return Result.Failure<KycStatusResponse>(
                    new Error("Error.Internal", "Verification service unavailable. Please try again."));
            }

            using var tokenDoc = JsonDocument.Parse(tokenJson);
            accessToken = tokenDoc.RootElement.GetProperty("accessToken").GetString() ?? string.Empty;
            if (string.IsNullOrEmpty(accessToken))
                throw new InvalidOperationException("Empty access token from QoreID");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obtaining QoreID token for user {UserId}", request.AuthUserId);
            kyc.Fail("Network error obtaining QoreID token");
            await _kycRepo.SaveChangesAsync(cancellationToken);
            return Result.Failure<KycStatusResponse>(
                new Error("Error.Internal", "Verification service unavailable. Please try again."));
        }

        // Step 2: Call QoreID NIN + Face verification endpoint
        QoreidResponse? qoreResponse = null;
        try
        {
            var http = _httpFactory.CreateClient();
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

            var payload = JsonSerializer.Serialize(new
            {
                idNumber = request.BVN,
                photoBase64 = request.PhotoBase64
            });

            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await http.PostAsync(
                $"{BaseUrl}/v1/ng/identities/face-verification/bvn", content, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogInformation("QoreID response for user {UserId}: {Status} body={Body}",
                request.AuthUserId, response.StatusCode, json);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("QoreID face-verification failed for user {UserId}: {Status} {Body}",
                    request.AuthUserId, response.StatusCode, json);

                var errMsg = TryGetErrorMessage(json);
                kyc.Fail($"QoreID error: {response.StatusCode}");
                await _kycRepo.SaveChangesAsync(cancellationToken);
                return Result.Success(new KycStatusResponse(false, "Failed",
                    errMsg ?? "NIN verification failed. Please check your NIN and try again."));
            }

            qoreResponse = JsonSerializer.Deserialize<QoreidResponse>(json,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
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

        // Check face match — QoreID returns summary.face_verification_check.match (bool) and match_score
        var faceCheck = qoreResponse.Summary?.FaceVerificationCheck;
        var matched   = faceCheck?.Match ?? false;
        var score     = faceCheck?.MatchScore ?? 0.0;

        if (!matched || score < MinConfidence)
        {
            kyc.Fail($"Face match failed: matched={matched} score={score}");
            await _kycRepo.SaveChangesAsync(cancellationToken);

            var faceMsg = !matched
                ? $"Your selfie did not match your NIN photo (score: {score:F0}%). Please retake in good lighting, facing the camera directly."
                : $"Face match confidence too low ({score:F0}%). Please retake your selfie.";

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
            nin: request.BVN,
            firstName: applicant?.Firstname,
            lastName: applicant?.Lastname,
            middleName: applicant?.Middlename,
            dateOfBirth: applicant?.Dob,
            gender: applicant?.Gender,
            nationality: "Nigerian",
            residenceState: null,
            photoPath: photoPath);

        await _kycRepo.SaveChangesAsync(cancellationToken);

        profile.Verify(photoPath);
        _profiles.Update(profile);
        await _profiles.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} verified via QoreID. Face confidence: {Confidence}",
            request.AuthUserId, score);

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
        public QoreidFaceVerificationCheck? FaceVerificationCheck { get; set; }
    }

    private class QoreidFaceVerificationCheck
    {
        public bool Match { get; set; }
        public double MatchScore { get; set; }
    }

    private class QoreidStatus
    {
        public string? State { get; set; }
        public string? Type { get; set; }
    }
}
