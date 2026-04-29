namespace Alexandria.Api.Features.Storage.Files.DeleteFile;

public sealed class DeleteFileRequest
{
    public Guid[] Ids { get; set; } = [];
    public bool HardDelete { get; set; } = false;
}