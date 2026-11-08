using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Services;
using Alexandria.Services.Preview.Images;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Alexandria.Services.Preview;

public sealed partial class CaptureMetadataService : ICaptureMetadataService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CaptureMetadataService> _logger;

    public CaptureMetadataService(
        IOptions<ImagePreviewConfig> options,
        IUnitOfWork unitOfWork,
        ILogger<CaptureMetadataService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;

        VipsRuntime.EnsureConfigured(options.Value);
    }

    /// <inheritdoc/>
    public async Task ExtractAndStoreAsync(Guid versionId, Stream source, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanSeek)
            throw new ArgumentException("Capture metadata source must be seekable.", nameof(source));

        var header = await Task.Run(() => CaptureHeaderReader.Read(source), ct);
        var metadata = CaptureMetadataMapper.ToEntity(versionId, header);

        await _unitOfWork.CaptureMetadata.UpsertAsync(metadata, ct);

        LogStored(_logger, versionId, metadata.CapturedAt.HasValue, metadata.Latitude.HasValue);
    }
}
