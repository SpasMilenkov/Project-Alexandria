using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.DocumentTextStats;
using FastEndpoints;
using Microsoft.AspNetCore.Mvc;

namespace Alexandria.Api.Features.Admin.DocumentTextStats;

/// <summary>
/// How far the document text sweep has got: outcome counts, documents still waiting and recent
/// failures. Failures name only the admin's own files.
/// </summary>
sealed class GetDocumentTextOverviewEndpoint(IDocumentTextStatsService statsService)
    : EndpointWithoutRequest<DocumentTextOverviewResponse>
{
    public override void Configure()
    {
        Get("/admin/document-text/overview");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(0, ResponseCacheLocation.None, noStore: true);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await statsService.GetOverviewAsync(User.GetUserId(), ct);
        await Send.OkAsync(result, ct);
    }
}