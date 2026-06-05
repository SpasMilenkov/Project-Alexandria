using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files.Streaming;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Streaming.GetHistoryByFile;

internal sealed class GetHistoryByFileRequest
{
    public Guid FileId { get; init; }
}

internal sealed class GetHistoryByFileValidator : Validator<GetHistoryByFileRequest>
{
    public GetHistoryByFileValidator()
    {
        RuleFor(x => x.FileId).NotEmpty();
    }
}

internal sealed class GetHistoryByFileEndpoint(IStreamHistoryService historyService)
    : Endpoint<GetHistoryByFileRequest, StreamHistoryDto?>
{
    public override void Configure()
    {
        Get("/stream-history/by-file");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetHistoryByFileRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await historyService.GetByFileAsync(req.FileId, userId, ct);

        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}