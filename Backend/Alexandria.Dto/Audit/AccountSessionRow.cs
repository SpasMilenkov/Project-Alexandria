namespace Alexandria.Dto.Audit;

/// <summary>A live refresh token reduced to who holds it and from which browser.</summary>
public sealed record AccountSessionRow(Guid UserId, string? UserAgent);
