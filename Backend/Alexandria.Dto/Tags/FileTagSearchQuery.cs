using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Tags;

public class FileTagSearchQuery
{
    public ICollection<Guid> TagIds { get; set; } = new List<Guid>();
    public TagMatchType MatchType { get; set; } = TagMatchType.Any;
    public int CurrentPage { get; set; } = 0;
    public int PageSize { get; set; } = 20;
    public long? MinFileSize { get; set; }
    public long? MaxFileSize { get; set; }
    public string? MimeTypePrefix { get; set; }

    // UTC instants, compared as given
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
}
