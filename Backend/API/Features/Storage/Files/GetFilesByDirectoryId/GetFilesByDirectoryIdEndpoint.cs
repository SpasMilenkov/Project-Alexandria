using System.Security.Claims;
using Common.Services;
using DTO.Files;
using FastEndpoints;
using FluentValidation;
using Models;
using Models.Enumerators;

namespace API.Features.Storage.Files.GetFilesByDirectoryId;

sealed class GetFilesByDirectoryIdRequest
{
    public Guid DirectoryId { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public SortBy SortBy { get; set; }
    public SortDirection SortDirection { get; set; }
}

sealed class GetFilesByDirectoryIdRequestValidator : Validator<GetFilesByDirectoryIdRequest>
{
    public GetFilesByDirectoryIdRequestValidator()
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

sealed class GetFilesByDirectoryIdEndpoint(IFileService fileService)
    : Endpoint<GetFilesByDirectoryIdRequest, PaginatedResult<FileResult>>
{
    public override void Configure()
    {
        Get("/files/directory/{directoryId}");
    }

    public override async Task HandleAsync(GetFilesByDirectoryIdRequest req, CancellationToken ct)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value
                           ?? throw new UnauthorizedAccessException("User ID not found in token");
        var userId = Guid.Parse(userIdString);
        var result = await fileService.GetFilesByDirectoryId(req.DirectoryId, userId, req.Page, req.PageSize,
            req.SortBy, req.SortDirection, ct);
        await Send.OkAsync(result, ct);
    }
}