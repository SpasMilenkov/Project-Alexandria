namespace Alexandria.Data.Models.Enumerators;

// A refresh token only rotates through the endpoint family that issued it, so a browser session
// can never be turned into readable tokens and a native one never lands in a cookie.
public enum SessionClient
{
    Browser,
    Native
}