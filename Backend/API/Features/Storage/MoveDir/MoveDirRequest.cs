namespace API.Features.Storage.MoveDir;

public class MoveDirRequest
{
    public Guid DirectoryId { get; set; }
    public Guid DestinationId { get; set; }
}