using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Domain.Repositories;

namespace TagAlong.User.API.Commands;

public record ProcessQoreidWebhookCommand(
    string RawBody,
    string? Signature,
    string WebhookSecret) : ICommand<WebhookResult>;

public record WebhookResult(bool Processed, string Message);

public class ProcessQoreidWebhookCommandHandler
    : ICommandHandler<ProcessQoreidWebhookCommand, WebhookResult>
{
    private readonly IKycVerificationRepository _kycRepo;
    private readonly IUserProfileRepository _profiles;
    private readonly ILogger<ProcessQoreidWebhookCommandHandler> _logger;

    public ProcessQoreidWebhookCommandHandler(
        IKycVerificationRepository kycRepo,
        IUserProfileRepository profiles,
        ILogger<ProcessQoreidWebhookCommandHandler> logger)
    {
        _kycRepo = kycRepo;
        _profiles = profiles;
        _logger = logger;
    }

    public async Task<Result<WebhookResult>> Handle(
        ProcessQoreidWebhookCommand request, CancellationToken cancellationToken)
    {
        // Always verify the HMAC signature — fail closed if secret is unconfigured or signature is absent/wrong
        if (string.IsNullOrEmpty(request.WebhookSecret))
        {
            _logger.LogError("QoreID webhook received but QoreId:WebhookSecret is not configured — rejecting all requests");
            return Result.Success(new WebhookResult(false, "Webhook secret not configured"));
        }
        if (string.IsNullOrEmpty(request.Signature) || !VerifySignature(request.RawBody, request.Signature, request.WebhookSecret))
        {
            _logger.LogWarning("QoreID webhook signature missing or invalid — payload rejected");
            return Result.Success(new WebhookResult(false, "Invalid signature"));
        }

        QoreidWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<QoreidWebhookPayload>(request.RawBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize QoreID webhook payload");
            return Result.Success(new WebhookResult(false, "Invalid payload"));
        }

        if (payload?.Data == null)
        {
            _logger.LogWarning("QoreID webhook missing data field");
            return Result.Success(new WebhookResult(false, "Missing data"));
        }

        var reference = payload.Data.Reference;
        if (string.IsNullOrEmpty(reference))
        {
            _logger.LogWarning("QoreID webhook missing reference");
            return Result.Success(new WebhookResult(false, "Missing reference"));
        }

        _logger.LogInformation("QoreID webhook received: event={Event} reference={Ref} state={State}",
            payload.Event, reference, payload.Data.Status?.State);

        // Look up the KYC record by the reference we passed when calling QoreID
        var kyc = await _kycRepo.GetByQoreIdReferenceAsync(reference, cancellationToken);
        if (kyc == null)
        {
            _logger.LogWarning("QoreID webhook: no KYC record found for reference {Ref}", reference);
            return Result.Success(new WebhookResult(false, "Reference not found"));
        }

        // If already completed, skip (idempotent)
        if (kyc.Status == KycStatus.Completed)
            return Result.Success(new WebhookResult(true, "Already processed"));

        var state = payload.Data.Status?.State ?? string.Empty;
        var ninStatus = payload.Data.Summary?.NinCheck?.Status ?? string.Empty;
        var faceStatus = payload.Data.Summary?.FaceCheck?.Status ?? string.Empty;
        var confidence = payload.Data.Summary?.FaceCheck?.Confidence ?? 0.0;

        var isVerifiedMatch = state.Equals("VERIFIED_MATCH", StringComparison.OrdinalIgnoreCase)
            || (ninStatus.Equals("VERIFIED", StringComparison.OrdinalIgnoreCase)
                && faceStatus.Equals("MATCH", StringComparison.OrdinalIgnoreCase)
                && confidence >= 70.0);

        if (!isVerifiedMatch)
        {
            var reason = state.Equals("VERIFIED_NO_MATCH", StringComparison.OrdinalIgnoreCase)
                ? $"Face match failed via webhook (confidence: {confidence:F0}%)"
                : $"NIN/face verification failed via webhook: state={state}";

            kyc.Fail(reason);
            _kycRepo.Update(kyc);
            await _kycRepo.SaveChangesAsync(cancellationToken);
            _logger.LogWarning("QoreID webhook: verification failed for reference {Ref} — {Reason}", reference, reason);
            return Result.Success(new WebhookResult(true, "Processed: failed"));
        }

        // Mark KYC complete
        var applicant = payload.Data.Applicant;
        kyc.Complete(
            nin: kyc.NIN ?? string.Empty,
            firstName: applicant?.Firstname,
            lastName: applicant?.Lastname,
            middleName: applicant?.Middlename,
            dateOfBirth: applicant?.Dob,
            gender: applicant?.Gender,
            nationality: "Nigerian",
            residenceState: null,
            photoPath: kyc.PhotoPath);

        _kycRepo.Update(kyc);

        // Mark the user profile as verified
        var profile = await _profiles.GetByAuthUserIdAsync(kyc.AuthUserId, cancellationToken);
        if (profile != null && !profile.IsVerified)
        {
            profile.Verify(kyc.PhotoPath);
            _profiles.Update(profile);
        }

        await _kycRepo.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("QoreID webhook: user {UserId} verified via webhook (ref={Ref}, confidence={Conf}%)",
            kyc.AuthUserId, reference, confidence);

        return Result.Success(new WebhookResult(true, "Processed: verified"));
    }

    private static bool VerifySignature(string body, string signature, string secret)
    {
        try
        {
            // Strip "sha256=" prefix if present
            var sigValue = signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
                ? signature[7..]
                : signature;

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
            var computed = Convert.ToHexString(hash).ToLowerInvariant();
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computed),
                Encoding.UTF8.GetBytes(sigValue.ToLowerInvariant()));
        }
        catch
        {
            return false;
        }
    }

    // ── Webhook payload models ──

    private class QoreidWebhookPayload
    {
        public string? Event { get; set; }
        public string? Timestamp { get; set; }
        public QoreidWebhookData? Data { get; set; }
    }

    private class QoreidWebhookData
    {
        public string? Reference { get; set; }
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

    private class QoreidCheck { public string? Status { get; set; } }

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
