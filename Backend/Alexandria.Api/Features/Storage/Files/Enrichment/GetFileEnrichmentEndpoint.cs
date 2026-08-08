using System.Text.Json.Nodes;
using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common;
using Alexandria.Dto.Enrichment;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Files.Enrichment;

internal sealed class GetFileEnrichmentRequest
{
    public Guid FileId { get; set; }
}

/// <summary>
/// A file's audio-analysis history for its owner or an admin.
/// </summary>
internal sealed class GetFileEnrichmentEndpoint(IUnitOfWork unitOfWork)
    : Endpoint<GetFileEnrichmentRequest, GetFileEnrichmentResponse>
{
    public override void Configure()
    {
        Get("/files/{fileId}/enrichment");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetFileEnrichmentRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var file = await unitOfWork.Files.GetByIdAsync(req.FileId, ct);
        if (file is null)
        {
            ThrowError("File was not found.", 404);
            return;
        }

        var isOwner = file.OwnerId == userId;
        if (!isOwner)
        {
            ThrowError("File was not found.", 404);
            return;
        }

        var enrichments = (await unitOfWork.FileEnrichments.FindAsync(e => e.FileId == req.FileId, ct)).ToList();
        var fileBatchFiles = (await unitOfWork.EssentiaBatchFiles.FindAsync(f => f.FileId == req.FileId, ct)).ToList();

        var batchIdToStatus = new Dictionary<Guid, string>();
        foreach (var batchId in fileBatchFiles.Select(f => f.BatchId).Distinct())
        {
            var batch = await unitOfWork.EssentiaBatches.GetByIdAsync(batchId, ct);
            if (batch is not null)
                batchIdToStatus[batchId] = batch.Status.ToString();
        }

        var response = new GetFileEnrichmentResponse
        {
            FileId = req.FileId,
            Enrichments = enrichments
                .GroupBy(e => e.Analyzer)
                .Select(g => g.OrderByDescending(e => e.CreatedAt).First())
                .OrderBy(e => e.Analyzer)
                .Select(e => new EnrichmentRowDto
                {
                    Analyzer = e.Analyzer,
                    Version = e.Version,
                    Payload = JsonNode.Parse(e.PayloadJson) ?? JsonNode.Parse("{}")!,
                    CreatedAt = e.CreatedAt,
                })
                .ToList(),
            Batches = fileBatchFiles
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new BatchRowDto
                {
                    BatchId = f.BatchId,
                    BatchStatus = batchIdToStatus.GetValueOrDefault(f.BatchId, "Unknown"),
                    FileStatus = f.Status.ToString(),
                    ErrorDetail = f.ErrorDetail,
                    CreatedAt = f.CreatedAt,
                })
                .ToList(),
        };

        await Send.OkAsync(response, ct);
    }
}