using System.Text.Json;
using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Exceptions.Policies;
using Alexandria.Common.Policies;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Enrichment;
using Alexandria.Dto.Files.Streaming;
using Alexandria.Dto.Policies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DirectoryNotFoundException = Alexandria.Common.Exceptions.Directories.DirectoryNotFoundException;

namespace Alexandria.Services.Storage.Policies;

public sealed partial class DirectoryPolicyService(
    IUnitOfWork unitOfWork,
    IDirectoryService directoryService,
    IOutbox outbox,
    ILogger<DirectoryPolicyService> logger,
    IOptions<ProcessingCapabilities> capabilities)
    : IDirectoryPolicyService
{
    private static readonly JsonSerializerOptions BackfillJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly bool _autoTaggingEnabled = capabilities.Value.AutoTaggingActive;
    private readonly bool _streamingEnabled = capabilities.Value.AdaptiveStreaming;

    public async Task<DirectoryPolicyDto?> GetPolicyAsync(Guid directoryId, Guid ownerId,
        CancellationToken ct = default)
    {
        if (!await directoryService.DirectoryExistsWithOwnershipAsync(directoryId, ownerId, ct))
            throw new DirectoryNotFoundException(directoryId);

        return await unitOfWork.DirectoryPolicies.GetByDirectoryIdAsync(directoryId, ct);
    }

    public async Task<DirectoryPolicyDto> CreatePolicyAsync(
        CreateDirectoryPolicyRequest request,
        Guid ownerId,
        CancellationToken ct = default)
    {
        var exists = await unitOfWork.DirectoryPolicies.ExistsForDirectoryAsync(request.DirectoryId, ct);
        if (exists)
            throw new InvalidOperationException($"A policy already exists for directory {request.DirectoryId}.");

        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var entity = request.ToEntity(ownerId);
            await unitOfWork.DirectoryPolicies.AddAsync(entity, ct);

            // A commit nested in the caller's transaction doesn't save, and rules added next
            // look the policy up in the database
            await unitOfWork.SaveChangesAsync(ct);
            await unitOfWork.CommitAsync(ct);

            LogPolicyCreated(logger, entity.Id, request.DirectoryId);

            return DirectoryPolicyDto.FromEntity(entity);
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackAsync(ct);
            LogPolicyCreateFailed(logger, ex, request.DirectoryId);
            throw;
        }
    }

    public async Task<DirectoryPolicyDto> UpdatePolicyAsync(
        Guid policyId,
        bool inheritedByChildren,
        Guid updatedBy,
        CancellationToken ct = default)
    {
        var policy = await unitOfWork.DirectoryPolicies.GetByIdAsync(policyId, ct)
                     ?? throw new DirectoryPolicyNotFoundException(policyId);

        var stopsSharing = policy.InheritedByChildren && !inheritedByChildren;

        policy.InheritedByChildren = inheritedByChildren;
        policy.UpdatedBy = updatedBy;

        await unitOfWork.InTransactionAsync(async () =>
        {
            unitOfWork.DirectoryPolicies.Update(policy);
            await unitOfWork.SaveChangesAsync(ct);

            var cutoff = new DateTimeOffset(policy.UpdatedAt ?? DateTime.UtcNow);

            foreach (var rule in policy.Rules.Where(r => r.DeletedAt == null))
                await EnqueueBackfillAsync(rule.ActionType, policyId, rule.Id, cutoff, ct);

            if (stopsSharing) await EnqueueFallbackBackfillAsync(policy.DirectoryId, ct);
        }, ct);

        LogPolicyUpdated(logger, policyId);

        return DirectoryPolicyDto.FromEntity(policy);
    }

    public async Task DeletePolicyAsync(Guid policyId, Guid requestedBy, CancellationToken ct = default)
    {
        var policy = await unitOfWork.DirectoryPolicies.GetByIdAsync(policyId, ct)
                     ?? throw new DirectoryPolicyNotFoundException(policyId);

        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            foreach (var rule in policy.Rules)
                unitOfWork.PolicyRules.Remove(rule);

            unitOfWork.DirectoryPolicies.Remove(policy);
            await EnqueueFallbackBackfillAsync(policy.DirectoryId, ct);

            await unitOfWork.CommitAsync(ct);

            LogPolicyDeleted(logger, policyId);
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackAsync(ct);
            LogPolicyDeleteFailed(logger, ex, policyId);
            throw;
        }
    }

    public async Task<PolicyRuleDto> AddRuleAsync(
        Guid policyId,
        CreatePolicyRuleRequest request,
        Guid updatedBy,
        CancellationToken ct = default)
    {
        var policyExists = await unitOfWork.DirectoryPolicies.ExistsAsync(p => p.Id == policyId, ct);
        if (!policyExists)
            throw new DirectoryPolicyNotFoundException(policyId);

        var entity = request.ToEntity(policyId, updatedBy);

        await unitOfWork.InTransactionAsync(async () =>
        {
            await unitOfWork.PolicyRules.AddAsync(entity, ct);
            await unitOfWork.SaveChangesAsync(ct);

            await EnqueueBackfillAsync(entity.ActionType, policyId, entity.Id,
                new DateTimeOffset(entity.CreatedAt), ct);
        }, ct);

        LogRuleAdded(logger, entity.Id, policyId);

        return PolicyRuleDto.FromEntity(entity);
    }

    public async Task<PolicyRuleDto> UpdateRuleAsync(
        Guid ruleId,
        UpdatePolicyRuleRequest request,
        Guid updatedBy,
        CancellationToken ct = default)
    {
        var rule = await unitOfWork.PolicyRules.GetByIdAsync(ruleId, ct)
                   ?? throw new PolicyRuleNotFoundException(ruleId);

        rule.TriggerType = request.TriggerType;
        rule.TriggerValue = request.TriggerValue;
        rule.Priority = request.Priority;
        rule.ApplyOnNewVersion = request.ApplyOnNewVersion;
        rule.Parameters = request.Parameters;
        rule.UpdatedBy = updatedBy;

        await unitOfWork.InTransactionAsync(async () =>
        {
            unitOfWork.PolicyRules.Update(rule);
            await unitOfWork.SaveChangesAsync(ct);

            await EnqueueBackfillAsync(rule.ActionType, rule.PolicyId, rule.Id,
                new DateTimeOffset(rule.UpdatedAt ?? DateTime.UtcNow), ct);
        }, ct);

        LogRuleUpdated(logger, ruleId);

        return PolicyRuleDto.FromEntity(rule);
    }

    public async Task DeleteRuleAsync(Guid ruleId, Guid requestedBy, CancellationToken ct = default)
    {
        var rule = await unitOfWork.PolicyRules.GetByIdAsync(ruleId, ct)
                   ?? throw new PolicyRuleNotFoundException(ruleId);

        await unitOfWork.InTransactionAsync(async () =>
        {
            unitOfWork.PolicyRules.Remove(rule);
            await unitOfWork.SaveChangesAsync(ct);

            if (await unitOfWork.PolicyRules.ExistsAsync(r => r.PolicyId == rule.PolicyId, ct)) return;

            var policy = await unitOfWork.DirectoryPolicies.GetByIdAsync(rule.PolicyId, ct);
            if (policy is not null) await EnqueueFallbackBackfillAsync(policy.DirectoryId, ct);
        }, ct);

        LogRuleDeleted(logger, ruleId);
    }

    /// <summary>
    /// A folder whose policy is deleted or loses its last rule falls back to the nearest inherited
    /// policy above it, and so do its subfolders when its policy stops being inherited. Those files
    /// predate that policy's rules, so each of them sweeps again; the sweeps skip files that are
    /// already handled, so the rest of that policy's scope is unchanged.
    /// </summary>
    private async Task EnqueueFallbackBackfillAsync(Guid directoryId, CancellationToken ct)
    {
        var fallback = await unitOfWork.DirectoryPolicies.ResolveInheritedPolicyAboveAsync(directoryId, ct);
        if (fallback is null) return;

        var cutoff = DateTimeOffset.UtcNow;

        foreach (var rule in fallback.Rules)
            await EnqueueBackfillAsync(rule.ActionType, fallback.Id, rule.Id, cutoff, ct);

        LogFallbackBackfillQueued(logger, fallback.Id, directoryId);
    }

    /// <summary>
    /// Every rule change sweeps the files that predate it, so a rule covers what is already in its
    /// folders and not only later uploads. Enqueued in the same transaction as the edit, so a saved
    /// edit always gets its backfill and a broker outage only delays it. Backup has no job to run.
    /// </summary>
    private async Task EnqueueBackfillAsync(PolicyActionType actionType, Guid policyId, Guid ruleId,
        DateTimeOffset cutoff, CancellationToken ct)
    {
        if (actionType == PolicyActionType.AutoTag)
        {
            await EnqueueEnrichmentBackfillAsync(new EnrichmentBackfillRequest(policyId, ruleId, cutoff), ct);
            return;
        }

        if (actionType == PolicyActionType.Transcode)
        {
            await EnqueueTranscodeBackfillAsync(new TranscodeBackfillRequest
            {
                PolicyId = policyId,
                RuleId = ruleId,
                Cutoff = cutoff,
            }, ct);
        }
    }

    private async Task EnqueueEnrichmentBackfillAsync(EnrichmentBackfillRequest request, CancellationToken ct)
    {
        if (!_autoTaggingEnabled)
        {
            LogBackfillSkippedDisabled(logger, request.PolicyId);
            return;
        }

        await outbox.EnqueueAsync(
            EnrichmentBackfill.BackfillRoutingKey,
            JsonSerializer.SerializeToUtf8Bytes(request, BackfillJsonOptions),
            ct);
    }

    private async Task EnqueueTranscodeBackfillAsync(TranscodeBackfillRequest request, CancellationToken ct)
    {
        if (!_streamingEnabled)
        {
            LogTranscodeBackfillSkippedDisabled(logger, request.PolicyId);
            return;
        }

        await outbox.EnqueueAsync(
            TranscodeBackfill.BackfillRoutingKey,
            JsonSerializer.SerializeToUtf8Bytes(request, BackfillJsonOptions),
            ct);
    }
}