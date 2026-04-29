using File = Alexandria.Data.Models.File;

namespace Alexandria.Dto.Files;

public record ImagePreview(MemoryStream ImageStream, File Metadata);