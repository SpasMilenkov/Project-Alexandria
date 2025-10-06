using File = Models.File;

namespace Common.Services;

public record ImagePreview(MemoryStream ImageStream, File Metadata);