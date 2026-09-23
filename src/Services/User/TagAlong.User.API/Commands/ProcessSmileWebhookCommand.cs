using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.EventBus;
using TagAlong.User.API.Hubs;
using TagAlong.User.API.Services;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Domain.Repositories;
using NinCache = TagAlong.User.Domain.Entities.NinCache;

namespace TagAlong.User.API.Commands;

public record KycStatusChangedIntegrationEvent(
    Guid UserId,
    string Status,
    string? FailureReason) : IntegrationEvent;

public record ProcessSmileWebhookCommand(
    string RawBody,
    string ApiKey,
    string PartnerId,
    string? HeaderSignature = null,
    string? HeaderTimestamp = null,
    bool IsJobStatusResult = false) : ICommand<WebhookResult>;

public class ProcessSmileWebhookCommandHandler : ICommandHandler<ProcessSmileWebhookCommand, WebhookResult>
{
    private readonly IKycVerificationRepository _kycRepo;
    private readonly IUserProfileRepository _profiles;
    private readonly INinCacheRepository _ninCache;
    private readonly IEmailService _email;
    private readonly IHubContext<LocationHub, ILocationClient> _hub;
    private readonly ISmileWebhookLogRepository _webhookLog;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ProcessSmileWebhookCommandHandler> _logger;

    public ProcessSmileWebhookCommandHandler(
        IKycVerificationRepository kycRepo,
        IUserProfileRepository profiles,
        INinCacheRepository ninCache,
        IEmailService email,
        IHubContext<LocationHub, ILocationClient> hub,
        ISmileWebhookLogRepository webhookLog,
        IEventBus eventBus,
        ILogger<ProcessSmileWebhookCommandHandler> logger)
    {
        _kycRepo = kycRepo;
        _profiles = profiles;
        _ninCache = ninCache;
        _email = email;
        _hub = hub;
        _webhookLog = webhookLog;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<Result<WebhookResult>> Handle(ProcessSmileWebhookCommand request, CancellationToken cancellationToken)
    {
        SmileWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SmileWebhookPayload>(request.RawBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize Smile ID webhook payload");
            return Result.Success(new WebhookResult(false, "Invalid payload"));
        }

        if (payload == null)
            return Result.Success(new WebhookResult(false, "Empty payload"));

        // SmileID puts sig+ts in Response-Signature/Response-Timestamp headers; fall back to JSON body fields
        var sigToVerify = !string.IsNullOrEmpty(request.HeaderSignature) ? request.HeaderSignature : payload.Signature;
        var tsToVerify  = !string.IsNullOrEmpty(request.HeaderTimestamp)  ? request.HeaderTimestamp  : payload.Timestamp;

        if (!string.IsNullOrEmpty(request.ApiKey) && !string.IsNullOrEmpty(tsToVerify))
        {
            if (!VerifySignature(sigToVerify, tsToVerify, request.PartnerId, request.ApiKey))
            {
                var snippet = request.RawBody.Length > 300 ? request.RawBody[..300] : request.RawBody;
                _logger.LogWarning(
                    "Smile ID webhook signature mismatch. ts={TS} pid={PID} sigLen={SL} fromHeader={FH} body={Body}",
                    tsToVerify, request.PartnerId, sigToVerify?.Length,
                    !string.IsNullOrEmpty(request.HeaderSignature), snippet);
                return Result.Success(new WebhookResult(false, "Invalid signature"));
            }
        }

        // Extract job_id from PartnerParams
        var jobId = payload.PartnerParams?.JobId;
        if (string.IsNullOrEmpty(jobId))
        {
            _logger.LogWarning("Smile ID webhook missing partner_params.job_id");
            return Result.Success(new WebhookResult(false, "Missing job_id"));
        }

        _logger.LogInformation("Smile ID webhook: job={JobId} resultCode={Code}", jobId, payload.ResultCode);

        var kyc = await _kycRepo.GetBySmileJobIdAsync(jobId, cancellationToken);
        if (kyc == null)
        {
            // Race condition: webhook can arrive before Flutter calls record-smile-job.
            // Wait briefly and retry once before giving up.
            await Task.Delay(3000, cancellationToken);
            kyc = await _kycRepo.GetBySmileJobIdAsync(jobId, cancellationToken);
        }
        if (kyc == null)
        {
            _logger.LogWarning("Smile ID webhook: no KYC record for job {JobId} (after retry)", jobId);
            await LogAsync(jobId, payload.ResultCode, null, request.IsJobStatusResult, "job-not-found", request.RawBody, cancellationToken);
            return Result.Success(new WebhookResult(false, "Job not found"));
        }

        if (kyc.Status == KycStatus.Completed)
        {
            await LogAsync(jobId, payload.ResultCode, kyc.AuthUserId, request.IsJobStatusResult, "already-processed", request.RawBody, cancellationToken);
            return Result.Success(new WebhookResult(true, "Already processed"));
        }

        // Result code 1210 = Verified match (Document KYC); 1220 = Failed match
        // 0810 = Biometric KYC Job Type 1 result — Approved when Verify_ID_Number=Verified
        // 1012 = ID data callback (always non-final)
        var resultCode = payload.ResultCode ?? string.Empty;

        // 1012 = ID data callback — always non-final, no action needed
        if (resultCode == "1012")
        {
            var smileUserId1012 = payload.PartnerParams?.UserId;
            if (!string.IsNullOrEmpty(smileUserId1012) && string.IsNullOrEmpty(kyc.SmileUserId))
            {
                kyc.SetSmileUserId(smileUserId1012);
                _kycRepo.Update(kyc);
                await _kycRepo.SaveChangesAsync(cancellationToken);
            }
            await LogAsync(jobId, resultCode, kyc.AuthUserId, request.IsJobStatusResult, "non-final-1012", request.RawBody, cancellationToken);
            return Result.Success(new WebhookResult(true, "Non-final callback — no action needed"));
        }

        // 0810 = Biometric KYC (Job Type 1) / NIN_V2.
        // When Verify_ID_Number=Verified this IS the final approved result — fall through to verify the user.
        // Without Verify_ID_Number=Verified it is a selfie-registered non-final callback (or biometric incomplete from poll).
        if (resultCode == "0810")
        {
            var ninApproved = string.Equals(payload.Actions?.VerifyIdNumber, "Verified", StringComparison.OrdinalIgnoreCase);
            if (!ninApproved)
            {
                if (request.IsJobStatusResult)
                {
                    var reason = "Your face scan couldn't be matched clearly enough. Please retry in a well-lit area.";
                    kyc.Fail(reason);
                    _kycRepo.Update(kyc);
                    await _kycRepo.SaveChangesAsync(cancellationToken);
                    var failProfile = await _profiles.GetByAuthUserIdAsync(kyc.AuthUserId, cancellationToken);
                    if (failProfile != null && !failProfile.IsVerified)
                    {
                        failProfile.ResetVerificationStatus();
                        _profiles.Update(failProfile);
                        await _profiles.SaveChangesAsync(cancellationToken);
                    }
                    _logger.LogWarning("SmileID job_status: job={JobId} 0810 without Verify_ID_Number=Verified — marking failed", jobId);
                    await PushKycStatusAsync(kyc.AuthUserId, "Failed", reason, cancellationToken);
                    await LogAsync(jobId, resultCode, kyc.AuthUserId, true, "biometric-incomplete", request.RawBody, cancellationToken);
                    return Result.Success(new WebhookResult(true, "Processed: biometric incomplete"));
                }
                var smileUserId0810 = payload.PartnerParams?.UserId;
                if (!string.IsNullOrEmpty(smileUserId0810) && string.IsNullOrEmpty(kyc.SmileUserId))
                {
                    kyc.SetSmileUserId(smileUserId0810);
                    _kycRepo.Update(kyc);
                    await _kycRepo.SaveChangesAsync(cancellationToken);
                }
                await LogAsync(jobId, resultCode, kyc.AuthUserId, request.IsJobStatusResult, "non-final-0810", request.RawBody, cancellationToken);
                return Result.Success(new WebhookResult(true, "Non-final callback — no action needed"));
            }
            _logger.LogInformation("SmileID: job={JobId} 0810 Approved (Verify_ID_Number=Verified) — processing as verified", jobId);
        }

        var ninVerified = string.Equals(payload.Actions?.VerifyIdNumber, "Verified", StringComparison.OrdinalIgnoreCase);
        // Accept human-review, selfie-to-authority (Passed/Completed), or Not Applicable
        var faceAction = payload.Actions?.HumanReviewCompare ?? payload.Actions?.SelfieToIdAuthorityCompare;
        var faceMatched = faceAction == null
                       || string.Equals(faceAction, "Passed", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(faceAction, "Completed", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(faceAction, "Not Applicable", StringComparison.OrdinalIgnoreCase);
        // 0810 with Verify_ID_Number=Verified is the approved result for Biometric KYC Job Type 1
        var isSuccess = resultCode == "1210" || resultCode == "0810";

        if (!isSuccess)
        {
            var reason = resultCode == "1220"
                ? "Your face did not match your NIN photo. Please try again in good lighting."
                : $"Verification could not be completed (code {resultCode}). Please try again.";
            kyc.Fail(reason);
            _kycRepo.Update(kyc);
            await _kycRepo.SaveChangesAsync(cancellationToken);
            var failProfile2 = await _profiles.GetByAuthUserIdAsync(kyc.AuthUserId, cancellationToken);
            if (failProfile2 != null && !failProfile2.IsVerified)
            {
                failProfile2.ResetVerificationStatus();
                _profiles.Update(failProfile2);
                await _profiles.SaveChangesAsync(cancellationToken);
            }
            _logger.LogWarning("Smile ID webhook: verification failed for job {JobId} — {Reason}", jobId, reason);
            await PushKycStatusAsync(kyc.AuthUserId, "Failed", reason, cancellationToken);
            await LogAsync(jobId, resultCode, kyc.AuthUserId, request.IsJobStatusResult, "failed", request.RawBody, cancellationToken);
            return Result.Success(new WebhookResult(true, "Processed: failed"));
        }

        var nin = kyc.NIN ?? payload.IdNumber ?? string.Empty;

        var (resolvedFirst, resolvedLast) = payload.ResolvedName();

        // SmileID has already validated the identity via biometric comparison — trust the result.
        var profile = await _profiles.GetByAuthUserIdAsync(kyc.AuthUserId, cancellationToken);

        kyc.Complete(
            nin: nin,
            firstName: resolvedFirst,
            lastName: resolvedLast,
            middleName: payload.MiddleName,
            dateOfBirth: payload.Dob,
            gender: payload.Gender,
            nationality: "Nigerian",
            residenceState: null,
            photoPath: kyc.PhotoPath);

        _kycRepo.Update(kyc);

        if (profile != null && !profile.IsVerified)
        {
            profile.Verify(kyc.PhotoPath);
            _profiles.Update(profile);
        }

        // Upsert NIN cache so future verifications of this NIN skip the SmileID call
        if (!string.IsNullOrEmpty(nin))
        {
            var cacheEntry = await _ninCache.GetByNinAsync(nin, cancellationToken);
            if (cacheEntry == null)
                await _ninCache.AddAsync(NinCache.Create(nin, resolvedFirst, resolvedLast,
                    payload.MiddleName, payload.Dob, payload.Gender), cancellationToken);
            else
                cacheEntry.Refresh(resolvedFirst, resolvedLast,
                    payload.MiddleName, payload.Dob, payload.Gender);
        }

        await _kycRepo.SaveChangesAsync(cancellationToken);

        await PushKycStatusAsync(kyc.AuthUserId, "Verified", null, cancellationToken);
        await LogAsync(jobId, resultCode, kyc.AuthUserId, request.IsJobStatusResult, "verified", request.RawBody, cancellationToken);
        _logger.LogInformation("Smile ID webhook: user {UserId} verified via job {JobId}", kyc.AuthUserId, jobId);

        return Result.Success(new WebhookResult(true, "Processed: verified"));
    }

    private async Task LogAsync(string? jobId, string? resultCode, Guid? authUserId, bool isJobStatusResult,
        string outcome, string rawBody, CancellationToken cancellationToken)
    {
        try
        {
            var entry = SmileWebhookLog.Create(jobId, resultCode, authUserId, isJobStatusResult, outcome, rawBody);
            await _webhookLog.AddAsync(entry, cancellationToken);
            await _webhookLog.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist SmileWebhookLog for job {JobId}", jobId);
        }
    }

    private async Task PushKycStatusAsync(Guid authUserId, string status, string? failureReason, CancellationToken cancellationToken)
    {
        try
        {
            await _hub.Clients.Group($"user_{authUserId}")
                .KycStatusChanged(status, failureReason);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push KycStatusChanged to user {UserId}", authUserId);
        }

        try
        {
            await _eventBus.PublishAsync(new KycStatusChangedIntegrationEvent(authUserId, status, failureReason));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish KycStatusChangedIntegrationEvent for user {UserId}", authUserId);
        }
    }

    private bool VerifySignature(string? signature, string timestamp, string partnerId, string apiKey)
    {
        if (string.IsNullOrEmpty(signature)) return false;
        try
        {
            var message = $"{timestamp}{partnerId}sid_request";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            var computed = Convert.ToBase64String(hash);
            // Trim in case of trailing whitespace or newlines in the payload value
            var received = signature.Trim();
            _logger.LogWarning("Webhook sig check: computed[0..8]={C} received[0..8]={R} ts={TS} pid={PID} keyLen={KL}",
                computed.Length >= 8 ? computed[..8] : computed,
                received.Length >= 8 ? received[..8] : received,
                timestamp, partnerId, apiKey.Length);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computed),
                Encoding.UTF8.GetBytes(received));
        }
        catch
        {
            return false;
        }
    }

    // ── Webhook payload models ──

    private class SmileWebhookPayload
    {
        public string? ResultCode { get; set; }
        public string? ResultText { get; set; }
        public string? SmileJobId { get; set; }
        public string? Signature { get; set; }
        public string? Timestamp { get; set; }
        public string? SmileClientId { get; set; }
        // SmileID may return individual fields or FullName depending on the ID type
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? MiddleName { get; set; }
        [JsonPropertyName("FullName")]
        public string? FullName { get; set; }
        [JsonPropertyName("DOB")]
        public string? Dob { get; set; }
        public string? Gender { get; set; }
        [JsonPropertyName("IDNumber")]
        public string? IdNumber { get; set; }
        public SmilePartnerParams? PartnerParams { get; set; }
        public SmileActions? Actions { get; set; }
        public string? IsFinalResult { get; set; }

        // Resolve first/last from either individual fields or FullName split
        public (string? first, string? last) ResolvedName()
        {
            if (!string.IsNullOrWhiteSpace(FirstName) || !string.IsNullOrWhiteSpace(LastName))
                return (FirstName?.Trim(), LastName?.Trim());

            if (string.IsNullOrWhiteSpace(FullName)) return (null, null);

            // NIN FullName order is typically: SURNAME FIRSTNAME MIDDLENAME
            var parts = FullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return (parts[0], parts[0]);
            return (parts[1], parts[0]); // firstName = parts[1], lastName = parts[0]
        }
    }

    private class SmilePartnerParams
    {
        [JsonPropertyName("job_id")]
        public string? JobId { get; set; }
        [JsonPropertyName("user_id")]
        public string? UserId { get; set; }
        [JsonPropertyName("job_type")]
        public int? JobType { get; set; }
    }

    private class SmileActions
    {
        [JsonPropertyName("Verify_ID_Number")]
        public string? VerifyIdNumber { get; set; }
        [JsonPropertyName("Selfie_To_ID_Authority_Compare")]
        public string? SelfieToIdAuthorityCompare { get; set; }
        [JsonPropertyName("Human_Review_Compare")]
        public string? HumanReviewCompare { get; set; }
        [JsonPropertyName("Liveness_Check")]
        public string? LivenessCheck { get; set; }
        [JsonPropertyName("Selfie_Check")]
        public string? SelfieCheck { get; set; }
    }
}
