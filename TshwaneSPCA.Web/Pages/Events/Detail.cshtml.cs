using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TshwaneSPCA.Data;

namespace TshwaneSPCA.Web.Pages.Events;

public sealed class DetailModel(PublicData database) : PageModel
{
    public PublicAsset? Photo { get; private set; }
    public Announcement Entry { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var entry = await database.Announcements.AsNoTracking().SingleOrDefaultAsync(row =>
            row.Slug == slug && row.PublishedAt != null && row.PublishedAt <= now, cancellationToken);
        if (entry is null)
            return NotFound();

        Entry = entry;
        if (entry.ImageAssetId is Guid image)
            Photo = await database.Assets.AsNoTracking().SingleOrDefaultAsync(asset =>
                asset.Id == image && asset.Kind == AssetKind.Gallery && asset.PublishedAt != null, cancellationToken);
        return Page();
    }

    public static string FormatDate(long timestamp) =>
        DateTimeOffset.FromUnixTimeSeconds(timestamp).ToOffset(TimeSpan.FromHours(2)).ToString("d MMM yyyy, HH:mm 'SAST'");
}
