using System.Text;
using Alexandria.Common;
using Alexandria.Common.Exceptions.Policies;
using Alexandria.Common.Policies;
using Alexandria.Common.Services;
using Alexandria.Dto.Policies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DirectoryNotFoundException = Alexandria.Common.Exceptions.Directories.DirectoryNotFoundException;

namespace Alexandria.Services.Storage.Policies;

public sealed partial class DirectoryPolicyService(
    IUnitOfWork unitOfWork,
    IDirectoryService directoryService,
    IPublisherService publisher,
    IConfiguration configuration,
    ILogger<DirectoryPolicyService> logger)
    : IDirectoryPolicyService
{
    private readonly bool _autoTaggingEnabled =
        bool.TryParse(configuration["Features:Autotagging"], out var enabled) && enabled;
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
            await unitOfWork.CommitAsync(ct);

            LogPolicyCreated(logger, entity.Id, request.DirectoryId);

            // Never inline: the sweep runs in the worker process off this message.
            await EnqueueBackfillAsync(entity.Id, ct);

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

        policy.InheritedByChildren = inheritedByChildren;
        policy.UpdatedBy = updatedBy;

        unitOfWork.DirectoryPolicies.Update(policy);
        await unitOfWork.SaveChangesAsync(ct);

        LogPolicyUpdated(logger, policyId);

        // Update only widens or narrows inheritance; re-sweeping is idempotent.
        await EnqueueBackfillAsync(policyId, ct);

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

        await unitOfWork.PolicyRules.AddAsync(entity, ct);
        await unitOfWork.SaveChangesAsync(ct);

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

        unitOfWork.PolicyRules.Update(rule);
        await unitOfWork.SaveChangesAsync(ct);

        LogRuleUpdated(logger, ruleId);

        return PolicyRuleDto.FromEntity(rule);
    }

    public async Task DeleteRuleAsync(Guid ruleId, Guid requestedBy, CancellationToken ct = default)
    {
        var rule = await unitOfWork.PolicyRules.GetByIdAsync(ruleId, ct)
                   ?? throw new PolicyRuleNotFoundException(ruleId);

        unitOfWork.PolicyRules.Remove(rule);
        await unitOfWork.SaveChangesAsync(ct);

        LogRuleDeleted(logger, ruleId);
    }

    /// <summary>
    /// Fire-and-forget backfill request scoped to the policy. Skipped entirely while
    /// autotagging is disabled: with no consumer running, publishing would only burn a
    /// broker round trip in the request for a sweep that ends in (0, 0, 0). A broker
    /// failure must never fail policy creation: the sweep is a best-effort catch-up and
    /// the failure is logged with the policy id for operators.
    /// </summary>
    private async Task EnqueueBackfillAsync(Guid policyId, CancellationToken ct)
    {
        if (!_autoTaggingEnabled)
        {
            LogBackfillSkippedDisabled(logger, policyId);
            return;
        }

        try
        {
            await publisher.PublishAsync(
                Encoding.UTF8.GetBytes(policyId.ToString()),
                EnrichmentBackfill.BackfillRoutingKey);
        }
        catch (Exception ex)
        {
            LogBackfillEnqueueFailed(logger, ex, policyId);
        }
    }
}