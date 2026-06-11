namespace Alexandria.Common.Exceptions.SignedUrls;

public sealed class SignedUrlExpiredException(string token)
    : Exception($"Shared file link with token '{token}' has expired or been revoked.");