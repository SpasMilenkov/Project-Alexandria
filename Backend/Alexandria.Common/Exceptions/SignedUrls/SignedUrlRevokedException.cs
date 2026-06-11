namespace Alexandria.Common.Exceptions.SignedUrls;

public class SignedUrlRevokedException(string token)
    : Exception($"Shared file link with token '{token}' has been revoked");