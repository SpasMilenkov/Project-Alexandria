using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.Files.GetVersionsForFile;

sealed internal class GetVersionsForFileRequest
{
    public Guid FileId { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

sealed internal class GetVersionForFileRequestValidator : Validator<GetVersionsForFileRequest>
{
    public GetVersionForFileRequestValidator()
    {
        RuleFor(x => x.FileId)
            .NotNull()
            .WithMessage("File ID cannot be null")
            .NotEmpty()
            .WithMessage("File ID cannot be empty");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Page cannot be negative");

        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(10)
            .WithMessage("Page size cannot be less than 10");
    }
}

sealed internal class GetVersionsForFileEndpoint(IFileService fileService)
    : Endpoint<GetVersionsForFileRequest, PaginatedResult<FileVersionDto>>
{
    public override void Configure()
    {
        Get("/files/versions");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetVersionsForFileRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await Send.OkAsync(
            await fileService.GetVersionsForFile(fileId: req.FileId, userId: userId, page: req.Page,
                pageSize: req.PageSize, ct),
            ct);
    }
}