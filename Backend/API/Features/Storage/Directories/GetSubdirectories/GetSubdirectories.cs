using System.Security.Claims;
using Common.Services;
using DTO.Directories;
using DTO.Files;
using FastEndpoints;
using FluentValidation;
using Models;
using Models.Enumerators;

namespace API.Features.Storage.Directories.GetSubdirectories;

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
    }

    public override async Task HandleAsync(GetSubDirectoriesRequest req, CancellationToken ct)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value
                           ?? throw new UnauthorizedAccessException("User ID not found in token");

        var userId = Guid.Parse(userIdString);
        var result = await directoryService.GetPaginatedDirectories(req.DirectoryId,
            userId, req.Page, req.PageSize, req.SortDirection, req.SortBy, ct);

        await Send.OkAsync(result, ct);
    }
}