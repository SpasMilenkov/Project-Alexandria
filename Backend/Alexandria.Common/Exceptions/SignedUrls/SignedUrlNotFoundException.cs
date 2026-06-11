namespace Alexandria.Common.Exceptions.SignedUrls;

public sealed class SignedUrlNotFoundException(string token)
    : Exception($"Shared file link with token '{token}' was not found.");