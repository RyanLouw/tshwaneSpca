using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TshwaneSPCA.Data;

namespace TshwaneSPCA.Web.Pages.Events;

public sealed class IndexModel(PublicData database) : PageModel
{
    public bool Archive { get; private set; }
    public List<Announcement> Entries { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string? view, CancellationToken cancellationToken)
    {
        if (view is not (null or "archive"))
            return NotFound();

        Archive = view == "archive";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var published = database.Announcements.AsNoTracking()
            .Where(entry => entry.PublishedAt != null && entry.PublishedAt <= now);
        Entries = await (Archive
                ? published.Where(entry => entry.ArchivedAt <= now || entry.EndsAt <= now)
                : published.Where(entry => (entry.ArchivedAt == null || entry.ArchivedAt > now) &&
                                           (entry.EndsAt == null || entry.EndsAt > now)))
            .OrderByDescending(entry => entry.PublishedAt).ThenBy(entry => entry.Id)
            .ToListAsync(cancellationToken);
        return Page();
    }
}
