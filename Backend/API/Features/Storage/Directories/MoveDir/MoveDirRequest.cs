namespace API.Features.Storage.Directories.MoveDir;

public class MoveDirRequest
{
    public Guid DirectoryId { get; set; }
    public Guid DestinationId { get; set; }
}