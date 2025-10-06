using File = Models.File;

namespace DTO;

public record FileResult(Stream FileStream, File Metadata);