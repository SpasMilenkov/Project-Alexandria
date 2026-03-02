using File = Models.File;

namespace DTO.Files;

public record ImagePreview(MemoryStream ImageStream, File Metadata);