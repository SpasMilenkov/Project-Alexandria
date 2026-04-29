using Alexandria.Dto.Files;

namespace Alexandria.Dto.Directories;

public class RootContentSummaryDto
{
    public IEnumerable<DirectorySummaryDto> Directories { get; set; } = new List<DirectorySummaryDto>();
    public IEnumerable<FileSummary> Files { get; set; } = new List<FileSummary>();
}