using System.Numerics;

namespace DTO.Files;

public record FileVersionDto(Guid Id, long Size, string MimeType, int VersionNumber);