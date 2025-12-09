namespace API.Features.Storage.DeleteDir;

public class DeleteDirRequest
{
    public Guid Id { get; set; }
    public bool Force { get; set; }
}