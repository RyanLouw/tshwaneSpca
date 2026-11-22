using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TshwaneSPCA.Data;
using TshwaneSPCA.Web.Staff;

namespace TshwaneSPCA.Web.Pages.Staff;

[RequestSizeLimit(MediaLibrary.MaxFileBytes + 64 * 1024)]
[RequestFormLimits(MultipartBodyLengthLimit = MediaLibrary.MaxFileBytes, ValueLengthLimit = 4096, ValueCountLimit = 24)]
public sealed class MediaModel(PublicData database, MediaLibrary storage) : PageModel
{
    public List<PublicAsset> Items { get; private set; } = [];
    public Guid? Editing { get; private set; }
    [BindProperty] public MediaInput Form { get; set; } = new();
    [BindProperty] public IFormFile? Upload { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken cancellationToken)
    {
        await ReadListAsync(cancellationToken);
        if (id is Guid existing)
        {
            var asset = Items.FirstOrDefault(item => item.Id == existing);
            if (asset is null)
                return NotFound();
            Editing = existing;
            Form = new MediaInput
            {
                Kind = asset.Kind, Title = asset.Title, Description = asset.Description, AltText = asset.AltText,
                EditionDate = asset.EditionDate, Published = asset.PublishedAt != null, Archived = asset.Archived
            };
        }

        return Page();
    }

    public async Task<IActionResult> OnGetPreviewAsync(Guid id, CancellationToken cancellationToken)
    {
        var asset = await database.Assets.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (asset is null || storage.Open(asset) is not { } stream)
            return NotFound();
        return File(stream, asset.ContentType, asset.ContentType == "application/pdf" ? MediaLibrary.DownloadName(asset) : null);
    }

    public async Task<IActionResult> OnPostAsync(Guid? id, CancellationToken cancellationToken)
    {
        PublicAsset? asset = null;
        if (id is Guid existing)
        {
            asset = await database.Assets.FindAsync([existing], cancellationToken);
            if (asset is null)
                return NotFound();
            Editing = existing;
            Form.Kind = asset.Kind;
        }

        if (!Enum.IsDefined(Form.Kind))
            ModelState.AddModelError("Form.Kind", "Select a media type.");
        if (Form.Kind == AssetKind.Gallery && string.IsNullOrWhiteSpace(Form.AltText))
            ModelState.AddModelError("Form.AltText", "Add alternative text describing the photo.");
        if (Form.Kind == AssetKind.Newsletter && Form.EditionDate is null)
            ModelState.AddModelError("Form.EditionDate", "Enter the newsletter edition date.");
        if (asset is null && Upload is null)
            ModelState.AddModelError("Upload", "Select a file to upload.");
        if (!ModelState.IsValid)
            return await InvalidAsync(cancellationToken);

        string? storedName = null;
        if (asset is null)
        {
            var stored = await storage.StoreAsync(Upload!, Form.Kind, cancellationToken);
            if (stored is null)
            {
                ModelState.AddModelError("Upload", "Choose a matching PDF (up to 10 MB) or JPEG/PNG photo (up to 5 MB).");
                return await InvalidAsync(cancellationToken);
            }

            storedName = stored.Value.Name;
            asset = new PublicAsset
            {
                StorageName = storedName, ContentType = stored.Value.ContentType, Kind = Form.Kind, Title = ""
            };
            database.Assets.Add(asset);
        }

        asset.Title = Form.Title.Trim();
        asset.Description = Form.Description?.Trim();
        asset.AltText = asset.Kind == AssetKind.Gallery ? Form.AltText?.Trim() : null;
        asset.EditionDate = asset.Kind == AssetKind.Newsletter ? Form.EditionDate : null;
        asset.PublishedAt = Form.Published ? asset.PublishedAt ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : null;
        asset.Archived = asset.Kind == AssetKind.Newsletter && Form.Published && Form.Archived;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (storedName is not null)
                storage.Delete(storedName);
            throw;
        }
        return RedirectToPage(new { id = asset.Id });
    }

    private async Task<IActionResult> InvalidAsync(CancellationToken cancellationToken)
    {
        await ReadListAsync(cancellationToken);
        return Page();
    }

    private async Task ReadListAsync(CancellationToken cancellationToken) =>
        Items = await database.Assets.AsNoTracking().OrderBy(item => item.Kind).ThenBy(item => item.Title)
            .ToListAsync(cancellationToken);

    public sealed class MediaInput
    {
        public AssetKind Kind { get; set; }
        [Required, StringLength(180)] public string Title { get; set; } = "";
        [StringLength(500)] public string? Description { get; set; }
        [StringLength(240)] public string? AltText { get; set; }
        [DataType(DataType.Date)] public DateOnly? EditionDate { get; set; }
        public bool Published { get; set; }
        public bool Archived { get; set; }
    }
}
