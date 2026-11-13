using Alexandria.Common.Exceptions;

namespace Alexandria.Common.Services;

/// <summary>Profile pictures for the signed-in account.</summary>
public interface IUserAvatarService
{
    /// <summary>Stores a new picture, points the account at it and returns its public URL.</summary>
    /// <exception cref="InvalidOperationException">Over cap or unsupported image content.</exception>
    /// <exception cref="UserNotFoundException">The account no longer exists.</exception>
    Task<string> ReplaceAsync(Stream content, Guid userId, CancellationToken ct = default);

    /// <summary>Clears the account's picture and deletes its stored bytes.</summary>
    /// <exception cref="UserNotFoundException">The account no longer exists.</exception>
    Task RemoveAsync(Guid userId, CancellationToken ct = default);
}
