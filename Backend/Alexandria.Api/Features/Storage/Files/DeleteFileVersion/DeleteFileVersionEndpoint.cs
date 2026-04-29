using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.Files.DeleteFileVersion;

internal sealed class DeleteFileVersionRequest
{
    public Guid Id { get; set; }
}

internal sealed class DeleteFileVersionRequestValidator : Validator<DeleteFileVersionRequest>
{
    public DeleteFileVersionRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotNull()
            .WithMessage("File version ID cannot be null")
            .NotEmpty()
            .WithMessage("File version ID cannot be empty");
    }
}

internal sealed class DeleteFileVersionEndpoint(IFileService fileService) : Endpoint<DeleteFileVersionRequest>
{
    public override void Configure()
    {
        Delete("/files/versions/{id}");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(DeleteFileVersionRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await fileService.RemoveFileVersion(fileVersionId: req.Id, userId, ct);

        await Send.OkAsync(cancellation: ct);
    }
}