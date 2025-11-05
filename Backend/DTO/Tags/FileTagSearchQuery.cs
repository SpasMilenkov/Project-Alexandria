using Models.Enumerators;

namespace DTO.Tags;

public class FileTagSearchQuery
{
    public ICollection<Guid> TagIds { get; set; } = new List<Guid>();
    public TagMatchType MatchType { get; set; } = TagMatchType.Any;
    public Guid? UserId { get; set; }
    public int CurrentPage { get; set; } = 0;
    public int PageSize { get; set; } = 20;
    public long? MinFileSize { get; set; }
    public long? MaxFileSize { get; set; }
    public string? MimeTypePrefix { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
}