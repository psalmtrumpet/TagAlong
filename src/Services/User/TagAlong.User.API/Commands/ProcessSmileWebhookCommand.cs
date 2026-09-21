using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.User.API.Services;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Domain.Repositories;
using NinCache = TagAlong.User.Domain.Entities.NinCache;

namespace TagAlong.User.API.Commands;

public record ProcessSmileWebhookCommand(string RawBody, string ApiKey, string PartnerId) : ICommand<WebhookResult>;

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

        // Verify signature if API key is available
        if (!string.IsNullOrEmpty(request.ApiKey) && !string.IsNullOrEmpty(payload.Timestamp))
        {
            if (!VerifySignature(payload.Signature, payload.Timestamp, request.PartnerId, request.ApiKey))
            {
                _logger.LogWarning("Smile ID webhook signature mismatch. ts={TS} pid={PID} sigLen={SL}",
                    payload.Timestamp, request.PartnerId, payload.Signature?.Length);
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
            _logger.LogWarning("Smile ID webhook: no KYC record for job {JobId}", jobId);
            return Result.Success(new WebhookResult(false, "Job not found"));
        }

        if (kyc.Status == KycStatus.Completed)
            return Result.Success(new WebhookResult(true, "Already processed"));

        // Result code 1210 = Verified match; 1220 = Failed match; 1012 = ID data callback (ignore)
        var resultCode = payload.ResultCode ?? string.Empty;

        if (resultCode == "1012")
            return Result.Success(new WebhookResult(true, "ID data callback — no action needed"));

        var ninVerified = string.Equals(payload.Actions?.VerifyIdNumber, "Verified", StringComparison.OrdinalIgnoreCase);
        var faceMatched = string.Equals(payload.Actions?.HumanReviewCompare, "Passed", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(payload.Actions?.HumanReviewCompare, "Not Applicable", StringComparison.OrdinalIgnoreCase);
        var isSuccess = resultCode == "1210" && ninVerified;

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

        // Name check: profile name must match at least one direction against the NIN name
        var profile = await _profiles.GetByAuthUserIdAsync(kyc.AuthUserId, cancellationToken);
        if (profile != null && !NinNameMatcher.NamesMatch(
                profile.FirstName, profile.LastName, payload.FirstName, payload.LastName))
        {
            var reason = NinNameMatcher.BuildMismatchReason(
                profile.FirstName, profile.LastName, payload.FirstName, payload.LastName);
            kyc.Fail(reason);
            _kycRepo.Update(kyc);

            // Upsert NIN cache even on mismatch — data is still valid for future lookups
            if (!string.IsNullOrEmpty(nin))
            {
                var cacheEntry = await _ninCache.GetByNinAsync(nin, cancellationToken);
                if (cacheEntry == null)
                    await _ninCache.AddAsync(NinCache.Create(nin, payload.FirstName, payload.LastName,
                        payload.MiddleName, payload.Dob, payload.Gender), cancellationToken);
                else
                    cacheEntry.Refresh(payload.FirstName, payload.LastName,
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
            firstName: payload.FirstName,
            lastName: payload.LastName,
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
                await _ninCache.AddAsync(NinCache.Create(nin, payload.FirstName, payload.LastName,
                    payload.MiddleName, payload.Dob, payload.Gender), cancellationToken);
            else
                cacheEntry.Refresh(payload.FirstName, payload.LastName,
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
            var message = $"{timestamp}{partnerId}sid_response";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            var computed = Convert.ToBase64String(hash);
            // Trim in case of trailing whitespace or newlines in the payload value
            var received = signature.Trim();
            _logger.LogDebug("Webhook sig check: computed[0..7]={C} received[0..7]={R} ts={TS} pid={PID}",
                computed.Length >= 8 ? computed[..8] : computed,
                received.Length >= 8 ? received[..8] : received,
                timestamp, partnerId);
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
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? MiddleName { get; set; }
        public string? Dob { get; set; }
        public string? Gender { get; set; }
        public string? IdNumber { get; set; }
        public SmilePartnerParams? PartnerParams { get; set; }
        public SmileActions? Actions { get; set; }
        public bool? IsFinalResult { get; set; }
    }

    private class SmilePartnerParams
    {
        public string? JobId { get; set; }
        public string? UserId { get; set; }
        public string? JobType { get; set; }
    }

    private class SmileActions
    {
        public string? VerifyIdNumber { get; set; }
        public string? HumanReviewCompare { get; set; }
        public string? LivenessCheck { get; set; }
        public string? SelfieCheck { get; set; }
    }
}
