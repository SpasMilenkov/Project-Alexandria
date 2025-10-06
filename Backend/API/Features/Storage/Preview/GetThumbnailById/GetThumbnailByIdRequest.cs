namespace API.Features.Storage.Preview.GetThumbnailById;

public class GetThumbnailByIdRequest
{
    public Guid Id { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}