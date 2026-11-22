using System.Security.Claims;

namespace TshwaneSPCA.Web.Staff;

public static class StaffAccess
{
    public const string Administrator = "Administrator";
    public const string Content = "WebsiteCopy";
    public const string Media = "Media";
    public const string Events = "Events";
    public const string Enquiries = "Enquiries";
    public const string Subscribers = "Subscribers";

    public static IReadOnlyDictionary<string, string> Modules { get; } = new Dictionary<string, string>
    {
        [Content] = "Website copy",
        [Media] = "Media and PDFs",
        [Events] = "Events and announcements",
        [Enquiries] = "Enquiries",
        [Subscribers] = "Subscribers and CSV export"
    };

    public static bool Allows(ClaimsPrincipal user, string module) =>
        user.IsInRole(Administrator) || user.IsInRole(module);
}
