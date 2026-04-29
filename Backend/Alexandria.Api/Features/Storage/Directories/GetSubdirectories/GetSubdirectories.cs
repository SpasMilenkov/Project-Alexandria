using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Directories;
using Alexandria.Dto.Files;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.Directories.GetSubdirectories;

internal sealed class GetSubDirectoriesRequest
{
    public Guid DirectoryId { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public SortDirection SortDirection { get; set; }
    public SortBy SortBy { get; set; }
}

internal class GetSubDirectoriesRequestValidator : Validator<GetSubDirectoriesRequest>
{
    public GetSubDirectoriesRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .GreaterThan(0)
            .LessThanOrEqualTo(ValidationConstants.PaginationConstants.MaxPageSize);

        RuleFor(x => x.SortBy)
            .IsInEnum();

        RuleFor(x => x.SortDirection)
            .IsInEnum();
    }
}

internal sealed class GetSubdirectories(IDirectoryService directoryService)
    : Endpoint<GetSubDirectoriesRequest, PaginatedResult<DirectorySummaryDto>>
{
    public override void Configure()
    {
        Get("/directories/sub");
        Description(x => x.WithTags("Directories"));
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetSubDirectoriesRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var result = await directoryService.GetPaginatedDirectories(req.DirectoryId,
            userId, req.Page, req.PageSize, req.SortDirection, req.SortBy, ct);

        await Send.OkAsync(result, ct);
    }
}