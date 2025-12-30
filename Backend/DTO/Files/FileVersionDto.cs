using System.Numerics;

namespace DTO.Files;

public record FileVersionDto(Guid Id, BigInteger Size, string MimeType, int VersionNumber);