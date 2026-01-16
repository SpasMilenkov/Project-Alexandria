namespace API.Features.Storage.Directories.DeleteDir;

public class DeleteDirRequest
{
    public Guid Id { get; set; }
    public bool HardDelete { get; set; }
}