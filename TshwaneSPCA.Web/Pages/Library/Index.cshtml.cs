using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TshwaneSPCA.Data;

namespace TshwaneSPCA.Web.Pages.Library;

public sealed class IndexModel(PublicData database) : PageModel
{
    public List<PublicAsset> Items { get; private set; } = [];
    public AssetKind Kind { get; private set; }
    public string Title { get; private set; } = "";

    public async Task<IActionResult> OnGetAsync(string collection, CancellationToken cancellationToken)
    {
        switch (collection)
        {
            case "newsletters": Kind = AssetKind.Newsletter; Title = "Newsletter archive"; break;
            case "photos": Kind = AssetKind.Gallery; Title = "Photo gallery"; break;
            case "documents": Kind = AssetKind.Document; Title = "Documents and information"; break;
            default: return NotFound();
        }

        Items = await database.Assets.AsNoTracking().Where(item => item.Kind == Kind && item.PublishedAt != null)
            .OrderByDescending(item => item.EditionDate).ThenByDescending(item => item.PublishedAt).ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        return Page();
    }
}
