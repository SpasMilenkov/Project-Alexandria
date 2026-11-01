using Alexandria.Common.Services;
using Alexandria.Data.Models;
using FastEndpoints;
using Microsoft.AspNetCore.Mvc;

namespace Alexandria.Api.Features.Monitoring.GetOperationalStatus;

sealed class GetOperationalStatusEndpoint(IOperationalEventService service)
    : EndpointWithoutRequest<List<OperationalEvent>>
{
    public override void Configure()
    {
        Get("/monitoring/status/history");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(0, ResponseCacheLocation.None, noStore: true);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await service.GetCurrentStatusAsync(ct);
        await Send.OkAsync(result, ct);
    }
}