namespace API.Features.Storage.Directories.MoveDir;

public sealed class MoveDirRequest
{
    public required Guid[] DirectoryIds { get; set; }
    public Guid? DestinationId { get; set; }
}