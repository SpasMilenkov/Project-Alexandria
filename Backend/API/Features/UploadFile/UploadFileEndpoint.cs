using FastEndpoints;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using MinioConfig = API.Config.MinioConfig;

namespace API.Features.UploadFile;

public class UploadFileEndpoint(IMinioClient minio, IOptions<MinioConfig> options)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/files/upload");
        AllowAnonymous();
        AllowFileUploads();
        
        // Swagger documentation
        Summary(s =>
        {
            s.Summary = "Upload a file to MinIO storage";
            s.Description = "Uploads a file with optional metadata. Supports large files via streaming.";
            s.Responses[200] = "File uploaded successfully";
            s.Responses[400] = "Bad request - missing file or validation error";
            s.Responses[500] = "Internal server error";
        });
        
        // Configure for file upload in Swagger UI
        Options(x =>
        {
            x.WithMetadata(new
            {
                file = "Select file...",
                filename = "optional-custom-name.pdf",
                path = "optional/folder/path",
                expectedchecksum = "optional-sha256-hash"
            });
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var bucketName = options.Value.UploadBucket;
        Console.WriteLine($"Bucket: {bucketName}");
        
        // Ensure bucket exists and versioning is enabled
        await EnsureBucketExistsAsync(bucketName, ct);

        string? fileName = null;
        string? path = null;
        string? expectedChecksum = null;
        string contentType = "application/octet-stream";
        Stream? fileStream = null;

        // Parse the multipart form to extract metadata and file stream
        await foreach (var section in FormMultipartSectionsAsync(ct))
        {
            if (section.IsFormSection)
            {
                var fieldName = section.FormSection.Name.ToLowerInvariant();
                var fieldValue = await section.FormSection.GetValueAsync(ct);

                switch (fieldName)
                {
                    case "filename":
                        fileName = fieldValue;
                        break;
                    case "path":
                        path = fieldValue;
                        break;
                    case "expectedchecksum":
                        expectedChecksum = fieldValue;
                        break;
                }
            }
            else if (section.IsFileSection)
            {
                // Get file metadata
                fileName ??= section.FileSection.FileName;
                contentType = section.FileSection.Section.ContentType ?? contentType;
                // FileLength is not available, we'll use -1 for unknown size
                fileStream = section.FileSection.FileStream;
                
                // Process the file stream immediately
                break;
            }
        }

        if (fileStream == null)
        {
            ThrowError("No file found in the request.");
            return;
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            ThrowError("File name is required.");
            return;
        }

        // Construct object name
        var objectName = string.IsNullOrWhiteSpace(path)
            ? fileName
            : $"{path.TrimEnd('/')}/{fileName}";

        try
        {
            // Create a streaming checksum calculator
            using var checksumStream = new ChecksumCalculatingStream(fileStream);
            
            // Upload to MinIO using the checksum stream
            await minio.PutObjectAsync(new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithStreamData(checksumStream)
                .WithObjectSize(-1) // Unknown size - let MinIO determine it
                .WithContentType(contentType), ct);

            // Get the calculated checksum
            var calculatedChecksum = checksumStream.GetChecksum();

            // Verify checksum if provided
            if (!string.IsNullOrEmpty(expectedChecksum) &&
                !calculatedChecksum.Equals(expectedChecksum, StringComparison.OrdinalIgnoreCase))
            {
                // Clean up the uploaded object since checksum failed
                await minio.RemoveObjectAsync(new RemoveObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName), ct);
                
                ThrowError("Checksum mismatch: uploaded file does not match expected hash.");
                return;
            }

            // Get version info
            var stat = await minio.StatObjectAsync(
                new StatObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName), ct);

            await Send.OkAsync(new UploadFileResponse
            {
                ObjectName = objectName,
                Url = $"{bucketName}/{objectName}",
                Checksum = calculatedChecksum,
                VersionId = stat.VersionId,
                Size = stat.Size
            }, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Upload failed: {ex.Message}");
            ThrowError($"Upload failed: {ex.Message}");
        }
    }

    private async Task EnsureBucketExistsAsync(string bucketName, CancellationToken ct)
    {
        var found = await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName), ct);
        if (!found)
        {
            await minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName), ct);
        }

        // Enable versioning
        await minio.SetVersioningAsync(
            new SetVersioningArgs()
                .WithBucket(bucketName)
                .WithVersioningEnabled(), ct);
    }
}


