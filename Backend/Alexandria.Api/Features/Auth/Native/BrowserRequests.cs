namespace Alexandria.Api.Features.Auth.Native;

public static class BrowserRequests
{
    // Browsers attach these to every fetch and page script cannot remove them, while native HTTP
    // clients never send them. Refusing them keeps the token-returning endpoints out of reach of any
    // web page, including a compromised Alexandria tab, so HttpOnly cookies stay the only browser path.
    private static readonly string[] BrowserOnlyHeaders = ["Origin", "Sec-Fetch-Mode", "Sec-Fetch-Site"];

    public static bool IsFromBrowser(HttpRequest request)
    {
        return BrowserOnlyHeaders.Any(request.Headers.ContainsKey);
    }
}