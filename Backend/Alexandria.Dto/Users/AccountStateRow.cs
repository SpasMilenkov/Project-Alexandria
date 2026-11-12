namespace Alexandria.Dto.Users;

/// <summary>A live account as the access overview lists it.</summary>
public sealed record AccountStateRow(Guid Id, string Name, string Email, DateTimeOffset? LockoutEnd);
