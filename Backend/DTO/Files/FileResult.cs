using File = Models.File;

namespace DTO.Files;

public record FileResult(Stream FileStream, File Metadata);