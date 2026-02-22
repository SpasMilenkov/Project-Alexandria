using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Common;
using Common.Config;
using Common.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Storage.Promotions;

public class PromotionService : IPromotionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAmazonS3 _s3;
    private readonly IOptions<S3Config> _config;
    private readonly ILogger<PromotionService> _logger;

    public PromotionService(
        IUnitOfWork unitOfWork,
        IAmazonS3 s3,
        IOptions<S3Config> config,
        ILogger<PromotionService> logger)
    {
        _unitOfWork = unitOfWork;
        _s3 = s3;
        _config = config;
        _logger = logger;
    }

    public async Task<bool> TryPromoteContentObjectAsync(
        Guid contentObjectId,
        string tempObjectKey,
        CancellationToken ct = default)
    {
        try
        {
            var contentObject = await _unitOfWork.ContentObjects.GetByIdAsync(contentObjectId, ct);

            if (contentObject is null)
            {
                _logger.LogWarning(
                    "ContentObject {ContentObjectId} not found for promotion",
                    contentObjectId);
                return false;
            }

            if (contentObject.IsPromoted)
            {
                _logger.LogDebug(
                    "ContentObject {ContentObjectId} already promoted",
                    contentObjectId);
                return true;
            }

            contentObject.PromotionAttempts++;
            contentObject.LastPromotionAttemptAt = DateTime.UtcNow;

            _unitOfWork.ContentObjects.Update(contentObject);
            await _unitOfWork.SaveChangesAsync(ct);


            _logger.LogInformation(
                "Attempting to promote ContentObject {ContentObjectId} (attempt: {Attempt})",
                contentObjectId, contentObject.PromotionAttempts);

            try
            {
                await _s3.CopyObjectAsync(new CopyObjectRequest
                {
                    SourceBucket = _config.Value.TempBucket,
                    SourceKey = $"content/{tempObjectKey}",
                    DestinationBucket = _config.Value.UploadBucket,
                    DestinationKey = contentObject.StorageKey,
                    IfNoneMatch = "*" // Idempotent: only copy if doesn't exist
                }, ct);

                _logger.LogInformation(
                    "Successfully copied ContentObject {ContentObjectId} to permanent storage",
                    contentObjectId);
            }
            catch (AmazonS3Exception ex)
                when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                _logger.LogInformation(
                    "ContentObject {ContentObjectId} already exists in permanent storage",
                    contentObjectId);
            }

            contentObject.IsPromoted = true;
            contentObject.PromotedAt = DateTime.UtcNow;

            _unitOfWork.ContentObjects.Update(contentObject);

            await _unitOfWork.SaveChangesAsync(ct);


            _logger.LogInformation(
                "Successfully promoted ContentObject {ContentObjectId}",
                contentObjectId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to promote ContentObject {ContentObjectId}: {ErrorMessage}",
                contentObjectId, ex.Message);

            // Check if we've exceeded retry limit
            var contentObject = await _unitOfWork.ContentObjects.GetByIdAsync(contentObjectId, ct);
            if (contentObject?.PromotionAttempts >= 10)
            {
                await AlertPromotionFailureAsync(contentObjectId, contentObject.PromotionAttempts);
            }

            return false;
        }
    }

    private Task AlertPromotionFailureAsync(Guid contentObjectId, int attempts)
    {
        // TODO: Implement alerting (email, Slack, PagerDuty, etc.)
        _logger.LogCritical(
            "ALERT: ContentObject {ContentObjectId} failed promotion after {Attempts} attempts!",
            contentObjectId, attempts);

        throw new NotImplementedException("Alerting not yet implemented");
        // return Task.CompletedTask;
    }
}