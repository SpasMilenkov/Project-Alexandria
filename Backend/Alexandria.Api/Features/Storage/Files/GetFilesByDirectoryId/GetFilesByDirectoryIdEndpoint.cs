using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.Files.GetFilesByDirectoryId;

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
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetFilesByDirectoryIdRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var result = await fileService.GetFilesByDirectoryIdAsync(req.DirectoryId, userId, req.Page, req.PageSize,
            req.SortBy, req.SortDirection, ct);
        await Send.OkAsync(result, ct);
    }
}