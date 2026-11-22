using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace TshwaneSPCA.Web.PublicServices;

public static class NewsletterToken
{
    public static string Create() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token)));

    public static bool IsValid(string? token) => token is { Length: 43 } &&
        token.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    public static string Link(MailSettings settings, string action, string token) =>
        $"{settings.PublicUrl.TrimEnd('/')}/newsletter/preferences?action={action}&token={token}";
}
