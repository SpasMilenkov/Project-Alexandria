using System.Net;
using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Services;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Alexandria.Services.Storage.Promotions;

public class PromotionService(
    IUnitOfWork unitOfWork,
    IAmazonS3 s3,
    IOptions<S3Config> config,
    ILogger<PromotionService> logger) : IPromotionService
{
    public async Task<bool> TryPromoteContentObjectAsync(
        Guid contentObjectId,
        string tempObjectKey,
        CancellationToken ct = default)
    {
        try
        {
            var contentObject = await unitOfWork.ContentObjects.GetByIdAsync(contentObjectId, ct);

            if (contentObject is null)
            {
                logger.LogWarning(
                    "ContentObject {ContentObjectId} not found for promotion",
                    contentObjectId);
                return false;
            }

            if (contentObject.IsPromoted)
            {
                logger.LogDebug(
                    "ContentObject {ContentObjectId} already promoted",
                    contentObjectId);
                return true;
            }

            contentObject.PromotionAttempts++;
            contentObject.LastPromotionAttemptAt = DateTime.UtcNow;

            unitOfWork.ContentObjects.Update(contentObject);
            await unitOfWork.SaveChangesAsync(ct);


            logger.LogInformation(
                "Attempting to promote ContentObject {ContentObjectId} (attempt: {Attempt})",
                contentObjectId, contentObject.PromotionAttempts);

            try
            {
                await s3.CopyObjectAsync(new CopyObjectRequest
                {
                    SourceBucket = config.Value.TempBucket,
                    SourceKey = $"content/{tempObjectKey}",
                    DestinationBucket = config.Value.UploadBucket,
                    DestinationKey = contentObject.StorageKey,
                    IfNoneMatch = "*" // Idempotent: only copy if doesn't exist
                }, ct);

                logger.LogInformation(
                    "Successfully copied ContentObject {ContentObjectId} to permanent storage",
                    contentObjectId);
            }
            catch (AmazonS3Exception ex)
                when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                logger.LogInformation(
                    "ContentObject {ContentObjectId} already exists in permanent storage{Exception}",
                    contentObjectId, ex);
            }

            contentObject.IsPromoted = true;
            contentObject.PromotedAt = DateTime.UtcNow;

            unitOfWork.ContentObjects.Update(contentObject);

            await unitOfWork.SaveChangesAsync(ct);


            logger.LogInformation(
                "Successfully promoted ContentObject {ContentObjectId}",
                contentObjectId);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to promote ContentObject {ContentObjectId}: {ErrorMessage}",
                contentObjectId, ex.Message);

            // Check if we've exceeded retry limit
            var contentObject = await unitOfWork.ContentObjects.GetByIdAsync(contentObjectId, ct);
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
        logger.LogCritical(
            "ALERT: ContentObject {ContentObjectId} failed promotion after {Attempts} attempts!",
            contentObjectId, attempts);

        throw new NotImplementedException("Alerting not yet implemented");
    }
}