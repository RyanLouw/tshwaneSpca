using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TshwaneSPCA.Data;

using TshwaneSPCA.Web.Staff;

namespace TshwaneSPCA.Web.Pages.Staff;

[RequestSizeLimit(32_768)]
[RequestFormLimits(ValueLengthLimit = 8000, ValueCountLimit = 24)]
[Authorize(Policy = StaffAccess.Events)]
public sealed partial class EventsModel(PublicData database) : PageModel
{
    private static readonly TimeSpan SouthAfricaOffset = TimeSpan.FromHours(2);
    public List<Announcement> Items { get; private set; } = [];
    public List<PublicAsset> Photos { get; private set; } = [];
    public Announcement? Editing { get; private set; }
    [BindProperty] public EventInput Form { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(long? id, CancellationToken cancellationToken)
    {
        await LoadListsAsync(cancellationToken);
        if (!id.HasValue)
            return Page();

        Editing = Items.FirstOrDefault(item => item.Id == id);
        if (Editing is null)
            return NotFound();

        Form = new EventInput
        {
            Title = Editing.Title, Summary = Editing.Summary, Body = Editing.Body, Location = Editing.Location,
            StartsAt = LocalTime(Editing.StartsAt), EndsAt = LocalTime(Editing.EndsAt), ImageId = Editing.ImageAssetId,
            State = Editing.PublishedAt == null ? PublishState.Draft :
                Editing.ArchivedAt != null ? PublishState.Archived : PublishState.Published
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(long? id, CancellationToken cancellationToken)
    {
        if (id.HasValue)
        {
            Editing = await database.Announcements.FindAsync([id.Value], cancellationToken);
            if (Editing is null)
                return NotFound();
        }

        if (!Enum.IsDefined(Form.State))
            ModelState.AddModelError("Form.State", "Choose a valid publishing status.");
        if (Form.StartsAt.HasValue && Form.EndsAt.HasValue && Form.StartsAt > Form.EndsAt)
            ModelState.AddModelError("Form.EndsAt", "The end must be after the start.");
        if (Form.ImageId is Guid image && !await database.Assets.AnyAsync(
                row => row.Id == image && row.Kind == AssetKind.Gallery && row.PublishedAt != null, cancellationToken))
            ModelState.AddModelError("Form.ImageId", "Choose a published photo.");

        if (!ModelState.IsValid)
        {
            await LoadListsAsync(cancellationToken);
            return Page();
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var entry = Editing;
        if (entry is null)
        {
            var stem = NonSlugCharacters().Replace(Form.Title.Trim().ToLowerInvariant(), "-").Trim('-');
            var prefix = stem.Length > 60 ? stem[..60].TrimEnd('-') : stem;
            entry = new Announcement { Slug = $"{prefix}-{Guid.NewGuid():N}".TrimStart('-'), Title = "", Summary = "", Body = "" };
            database.Announcements.Add(entry);
        }

        entry.Title = Form.Title.Trim();
        entry.Summary = Form.Summary.Trim();
        entry.Body = Form.Body.Trim();
        entry.Location = Form.Location?.Trim();
        entry.ImageAssetId = Form.ImageId;
        if (Form.ImageId != null)
            entry.ImagePath = null;
        entry.StartsAt = UtcSeconds(Form.StartsAt);
        entry.EndsAt = UtcSeconds(Form.EndsAt);
        entry.PublishedAt = Form.State == PublishState.Draft ? null : entry.PublishedAt ?? now;
        entry.ArchivedAt = Form.State == PublishState.Archived ? entry.ArchivedAt ?? now : null;
        await database.SaveChangesAsync(cancellationToken);
        return RedirectToPage(new { id = entry.Id });
    }

    private async Task LoadListsAsync(CancellationToken cancellationToken)
    {
        Items = await database.Announcements.AsNoTracking().OrderByDescending(row => row.Id).ToListAsync(cancellationToken);
        Photos = await database.Assets.AsNoTracking().Where(row => row.Kind == AssetKind.Gallery && row.PublishedAt != null)
            .OrderBy(row => row.Title).ToListAsync(cancellationToken);
    }

    private static long? UtcSeconds(DateTime? value) => value is DateTime time
        ? new DateTimeOffset(DateTime.SpecifyKind(time, DateTimeKind.Unspecified), SouthAfricaOffset).ToUnixTimeSeconds()
        : null;

    private static DateTime? LocalTime(long? value) => value is long time
        ? DateTimeOffset.FromUnixTimeSeconds(time).ToOffset(SouthAfricaOffset).DateTime
        : null;

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugCharacters();

    public enum PublishState { Draft, Published, Archived }

    public sealed class EventInput
    {
        [Required, StringLength(180)] public string Title { get; set; } = "";
        [Required, StringLength(500)] public string Summary { get; set; } = "";
        [Required, StringLength(8000)] public string Body { get; set; } = "";
        [StringLength(180)] public string? Location { get; set; }
        public DateTime? StartsAt { get; set; }
        public DateTime? EndsAt { get; set; }
        public Guid? ImageId { get; set; }
        public PublishState State { get; set; }
    }
}
