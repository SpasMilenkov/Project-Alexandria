using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Library;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Library;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Directory = Alexandria.Data.Models.Directory;

namespace Alexandria.Services.Storage.Library;

public sealed partial class LibrarySetupService(
    IUnitOfWork unitOfWork,
    ILogger<LibrarySetupService> logger,
    IOptions<ProcessingCapabilities> capabilities)
    : ILibrarySetupService
{
    private readonly ProcessingCapabilities _capabilities = capabilities.Value;

    public async Task FinishTourAsync(Guid userId, IReadOnlyList<LibraryFolderChoice> folders,
        CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            // Advancing first takes the row lock, so a repeated submit waits here and then finds
            // the tour already finished instead of creating the folders a second time.
            var advanced = await unitOfWork.Users.AdvanceOnboardingAsync(
                userId, OnboardingStep.Tour, OnboardingStep.Done, ct);

            if (!advanced)
                throw new OnboardingStepMismatchException(userId, OnboardingStep.Tour);

            foreach (var choice in folders)
                await AddFolderAsync(userId, choice, ct);

            await unitOfWork.CommitAsync(ct);

            LogTourFinished(logger, userId, folders.Count);
        }
        catch (OnboardingStepMismatchException)
        {
            await unitOfWork.RollbackAsync(ct);
            LogTourAlreadyFinished(logger, userId);
            throw;
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackAsync(ct);
            LogTourFinishFailed(logger, ex, userId);
            throw;
        }
    }

    private async Task AddFolderAsync(Guid userId, LibraryFolderChoice choice, CancellationToken ct)
    {
        var preset = LibraryFolderPresets.Get(choice.Kind);

        // System folders can't be renamed later, so the tour can't name them either.
        var name = preset.SystemKind is null ? choice.Name.Trim() : preset.DefaultName;

        // A system folder the owner already has stays theirs, whatever name it was given.
        if (preset.SystemKind is { } systemKind &&
            await unitOfWork.Directories.ExistsAsync(d => d.OwnerId == userId && d.SystemKind == systemKind, ct))
            return;

        var existing = await unitOfWork.Directories.FirstOrDefaultAsync(
            d => d.Name == name && d.ParentId == null && d.OwnerId == userId && d.DeletedAt == null, ct);

        var directoryId = existing?.Id ?? Guid.NewGuid();

        if (existing is null)
        {
            await unitOfWork.Directories.AddAsync(new Directory
            {
                Id = directoryId,
                Name = name,
                OwnerId = userId,
                ParentId = null,
                SystemKind = preset.SystemKind,
            }, ct);
        }
        else if (preset.SystemKind is not null && existing.SystemKind is null)
        {
            existing.SystemKind = preset.SystemKind;
            unitOfWork.Directories.Update(existing);
        }

        var rules = LibraryPolicyRules.Build(preset, choice, _capabilities, userId);
        if (rules.Count == 0) return;

        if (existing is not null && await unitOfWork.DirectoryPolicies.ExistsForDirectoryAsync(directoryId, ct))
        {
            LogExistingPolicyKept(logger, directoryId, userId);
            return;
        }

        var policy = new DirectoryPolicy
        {
            Id = Guid.NewGuid(),
            DirectoryId = directoryId,
            InheritedByChildren = true,
            UpdatedBy = userId,
        };

        await unitOfWork.DirectoryPolicies.AddAsync(policy, ct);

        foreach (var rule in rules)
        {
            rule.PolicyId = policy.Id;
            await unitOfWork.PolicyRules.AddAsync(rule, ct);
        }
    }
}
