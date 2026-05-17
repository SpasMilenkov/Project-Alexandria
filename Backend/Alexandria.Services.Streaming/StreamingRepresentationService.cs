using Alexandria.Common;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Extensions;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming;

public partial class StreamingRepresentationService(
    IUnitOfWork uow,
    ILogger<StreamingRepresentationService> logger) : IStreamingRepresentationService
{
    /// <inheritdoc/>
    public async Task<StreamingRepresentationResponse> CreateRepresentationAsync(
        CreateStreamingRepresentationRequest request,
        CancellationToken ct = default)
    {
        var jobExists = await uow.TranspilationJobs.ExistsAsync(j => j.Id == request.JobId, ct);

        if (!jobExists)
            throw new TranspilationJobNotFoundException(request.JobId);

        var entity = request.ToEntity();
        var created = await uow.StreamingRepresentations.AddAsync(entity, ct);

        LogRepresentationCreated(logger, created.Id, request.JobId, request.Codec);

        return created.ToResponse();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<StreamingRepresentationResponse>> CreateRepresentationsAsync(
        List<CreateStreamingRepresentationRequest> requests,
        CancellationToken ct = default)
    {
        if (requests.Count == 0)
            return [];

        var jobId = requests[0].JobId;

        var jobExists = await uow.TranspilationJobs.ExistsAsync(j => j.Id == jobId, ct);
        if (!jobExists)
            throw new TranspilationJobNotFoundException(jobId);

        var entities = requests.Select(r => r.ToEntity());
        var created = await uow.StreamingRepresentations.AddRangeAsync(entities, ct);

        var list = created as StreamingRepresentation[] ?? created.ToArray();

        LogRepresentationsCreated(logger, list.Length, jobId);

        return list.Select(c => c.ToResponse());
    }

    /// <inheritdoc/>
    public async Task<StreamingRepresentationResponse> GetByIdAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var representation = await uow.StreamingRepresentations.GetByIdAsync(id, ct)
                             ?? throw new StreamingRepresentationNotFoundException(id);

        return representation.ToResponse();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<StreamingRepresentationResponse>> GetByJobIdAsync(
        Guid jobId,
        CancellationToken ct = default)
    {
        var representations = await uow.StreamingRepresentations.GetByJobIdAsync(jobId, ct);
        return representations.Select(r => r.ToResponse()).ToList();
    }

    /// <inheritdoc/>
    public async Task<PaginatedResult<StreamingRepresentationResponse>> FindRepresentationsAsync(
        StreamingRepresentationQuery query,
        CancellationToken ct = default)
    {
        var result = await uow.StreamingRepresentations.FindRepresentationsAsync(query, ct);

        return new PaginatedResult<StreamingRepresentationResponse>
        {
            Items = result.Items.Select(r => r.ToResponse()).ToList(),
            TotalCount = result.TotalCount,
            CurrentPage = result.CurrentPage,
            PageSize = result.PageSize,
            TotalPages = result.TotalPages
        };
    }

    /// <inheritdoc/>
    public async Task MarkReadyAsync(
        Guid representationId,
        CancellationToken ct = default)
    {
        var representation = await uow.StreamingRepresentations.GetByIdAsync(representationId, ct)
                             ?? throw new StreamingRepresentationNotFoundException(representationId);

        if (representation.Status is RepresentationStatus.Ready or RepresentationStatus.Failed)
            throw new InvalidRepresentationStateException(
                representationId, representation.Status, nameof(MarkReadyAsync));

        await uow.StreamingRepresentations.MarkReadyAsync(representationId, ct);

        LogRepresentationMarkedReady(logger, representationId);
    }

    /// <inheritdoc/>
    public async Task MarkFailedAsync(
        Guid representationId,
        CancellationToken ct = default)
    {
        var representation = await uow.StreamingRepresentations.GetByIdAsync(representationId, ct)
                             ?? throw new StreamingRepresentationNotFoundException(representationId);

        if (representation.Status is RepresentationStatus.Ready or RepresentationStatus.Failed)
            throw new InvalidRepresentationStateException(
                representationId, representation.Status, nameof(MarkFailedAsync));

        await uow.StreamingRepresentations.MarkFailedAsync(representationId, ct);

        LogRepresentationMarkedFailed(logger, representationId);
    }

    /// <inheritdoc/>
    public async Task MarkProcessingAsync(
        Guid representationId,
        CancellationToken ct = default)
    {
        var representation = await uow.StreamingRepresentations.GetByIdAsync(representationId, ct)
                             ?? throw new StreamingRepresentationNotFoundException(representationId);

        if (representation.Status is RepresentationStatus.Ready or RepresentationStatus.Failed)
            throw new InvalidRepresentationStateException(
                representationId, representation.Status, nameof(MarkProcessingAsync));

        await uow.StreamingRepresentations.MarkProcessingAsync(representationId, ct);

        LogRepresentationMarkedProcessing(logger, representationId);
    }

    /// <inheritdoc/>
    public async Task MarkAllReadyAsync(
        List<Guid> representationIds,
        CancellationToken ct = default)
    {
        await uow.StreamingRepresentations.MarkAllReadyAsync(representationIds, ct);
        LogRepresentationsMarkedReady(logger, representationIds.Count);
    }

    /// <inheritdoc/>
    public async Task MarkAllFailedAsync(
        List<Guid> representationIds,
        CancellationToken ct = default)
    {
        await uow.StreamingRepresentations.MarkAllFailedAsync(representationIds, ct);
        LogRepresentationsMarkedFailed(logger, representationIds.Count);
    }
}