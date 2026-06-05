using Alexandria.Common;
using Alexandria.Common.Exceptions.Policies;
using Alexandria.Common.Services;
using Alexandria.Dto.Policies;
using Microsoft.Extensions.Logging;
using DirectoryNotFoundException = Alexandria.Common.Exceptions.Directories.DirectoryNotFoundException;

namespace Alexandria.Services.Storage.Policies;

public sealed partial class DirectoryPolicyService(
    IUnitOfWork unitOfWork,
    IDirectoryService directoryService,
    ILogger<DirectoryPolicyService> logger)
    : IDirectoryPolicyService
{
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
}