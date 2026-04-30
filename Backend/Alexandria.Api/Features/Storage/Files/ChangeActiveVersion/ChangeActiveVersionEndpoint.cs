using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.Files.ChangeActiveVersion;

internal sealed class ChangeActiveVersionRequest
{
    public Guid FileId { get; set; }
    public Guid VersionId { get; set; }
}

internal sealed class ChangeActiveVersionRequestValidator : Validator<ChangeActiveVersionRequest>
{
    public ChangeActiveVersionRequestValidator()
    {
        RuleFor(x => x.FileId).NotEmpty().NotNull();
        RuleFor(x => x.VersionId).NotEmpty().NotNull();
    }
}

internal sealed class ChangeActiveVersionEndpoint(IFileService fileService) : Endpoint<ChangeActiveVersionRequest>
{
    public override void Configure()
    {
        Patch("/files/versions");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(ChangeActiveVersionRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        await fileService.ChangeActiveVersionAsync(versionId: req.VersionId, fileId: req.FileId, userId, ct);
        await Send.NoContentAsync(ct);
    }
}