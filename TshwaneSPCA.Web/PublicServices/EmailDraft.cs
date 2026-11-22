using Microsoft.AspNetCore.WebUtilities;

namespace TshwaneSPCA.Web.PublicServices;

public sealed record EmailDraft(string To, string Subject, string? Cc = null)
{
    public string Mailto => Compose($"mailto:{Uri.EscapeDataString(To).Replace("%40", "@", StringComparison.Ordinal)}",
        "subject", includeRecipient: false);

    public string Gmail => Compose("https://mail.google.com/mail/?view=cm&fs=1", "su");

    public string Outlook => Compose("https://outlook.live.com/mail/0/deeplink/compose", "subject");

    private string Compose(string url, string subjectParameter, bool includeRecipient = true)
    {
        var fields = new Dictionary<string, string?> { [subjectParameter] = Subject };
        if (includeRecipient)
            fields["to"] = To;
        if (!string.IsNullOrWhiteSpace(Cc))
            fields["cc"] = Cc;
        return QueryHelpers.AddQueryString(url, fields);
    }
}
