using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.User.API.Services;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Domain.Repositories;
using NinCache = TagAlong.User.Domain.Entities.NinCache;

namespace TagAlong.User.API.Commands;

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
    private readonly ILogger<ProcessSmileWebhookCommandHandler> _logger;

    public ProcessSmileWebhookCommandHandler(
        IKycVerificationRepository kycRepo,
        IUserProfileRepository profiles,
        INinCacheRepository ninCache,
        IEmailService email,
        ILogger<ProcessSmileWebhookCommandHandler> logger)
    {
        _kycRepo = kycRepo;
        _profiles = profiles;
        _ninCache = ninCache;
        _email = email;
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
            return Result.Success(new WebhookResult(false, "Job not found"));
        }

        if (kyc.Status == KycStatus.Completed)
            return Result.Success(new WebhookResult(true, "Already processed"));

        // Result code 1210 = Verified match; 1220 = Failed match; 1012 = ID data callback (ignore)
        var resultCode = payload.ResultCode ?? string.Empty;

        // 1012 = ID data callback, 0810 = selfie registered — both are non-final from webhooks.
        // From job_status (IsJobStatusResult=true), job_complete=true + 0810 means the biometric
        // comparison never ran — treat as a retriable failure rather than looping forever.
        if (resultCode == "1012" || resultCode == "0810")
        {
            if (request.IsJobStatusResult)
            {
                var reason = "Biometric comparison did not complete (SmileID returned code " + resultCode + "). Please try again.";
                kyc.Fail(reason);
                _kycRepo.Update(kyc);
                await _kycRepo.SaveChangesAsync(cancellationToken);
                _logger.LogWarning("SmileID job_status: job={JobId} stuck at {Code} — marking failed", jobId, resultCode);
                return Result.Success(new WebhookResult(true, "Processed: biometric incomplete"));
            }
            // From webhook: store the SmileID user_id so the status endpoint can poll get_job_status later.
            var smileUserId = payload.PartnerParams?.UserId;
            if (!string.IsNullOrEmpty(smileUserId) && string.IsNullOrEmpty(kyc.SmileUserId))
            {
                kyc.SetSmileUserId(smileUserId);
                _kycRepo.Update(kyc);
                await _kycRepo.SaveChangesAsync(cancellationToken);
            }
            return Result.Success(new WebhookResult(true, "Non-final callback — no action needed"));
        }

        var ninVerified = string.Equals(payload.Actions?.VerifyIdNumber, "Verified", StringComparison.OrdinalIgnoreCase);
        // Accept either a human-review comparison or the selfie-to-authority comparison passing
        var faceAction = payload.Actions?.HumanReviewCompare ?? payload.Actions?.SelfieToIdAuthorityCompare;
        var faceMatched = faceAction == null
                       || string.Equals(faceAction, "Passed", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(faceAction, "Not Applicable", StringComparison.OrdinalIgnoreCase);
        var isSuccess = resultCode == "1210";

        if (!isSuccess)
        {
            var reason = $"Smile ID result: code={resultCode} nin={payload.Actions?.VerifyIdNumber} face={payload.Actions?.HumanReviewCompare}";
            kyc.Fail(reason);
            _kycRepo.Update(kyc);
            await _kycRepo.SaveChangesAsync(cancellationToken);
            _logger.LogWarning("Smile ID webhook: verification failed for job {JobId} — {Reason}", jobId, reason);
            return Result.Success(new WebhookResult(true, "Processed: failed"));
        }

        var nin = kyc.NIN ?? payload.IdNumber ?? string.Empty;

        var (resolvedFirst, resolvedLast) = payload.ResolvedName();

        // Name check: profile name must match at least one direction against the NIN name
        var profile = await _profiles.GetByAuthUserIdAsync(kyc.AuthUserId, cancellationToken);
        if (profile != null && !NinNameMatcher.NamesMatch(
                profile.FirstName, profile.LastName, resolvedFirst, resolvedLast))
        {
            var reason = NinNameMatcher.BuildMismatchReason(
                profile.FirstName, profile.LastName, resolvedFirst, resolvedLast);
            kyc.Fail(reason);
            _kycRepo.Update(kyc);

            // Upsert NIN cache even on mismatch — data is still valid for future lookups
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

            await _email.SendAsync(
                profile.Email,
                $"{profile.FirstName} {profile.LastName}",
                "TagAlong — Identity Verification Failed",
                NinNameMatcher.BuildFailureEmailHtml(profile.FirstName, reason),
                cancellationToken);

            _logger.LogWarning("SmileID webhook: name mismatch for user {UserId} job {JobId}", kyc.AuthUserId, jobId);
            return Result.Success(new WebhookResult(true, "Processed: name mismatch"));
        }

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

        _logger.LogInformation("Smile ID webhook: user {UserId} verified via job {JobId}", kyc.AuthUserId, jobId);

        return Result.Success(new WebhookResult(true, "Processed: verified"));
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
