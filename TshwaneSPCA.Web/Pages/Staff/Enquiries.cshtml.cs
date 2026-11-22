using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TshwaneSPCA.Data;

namespace TshwaneSPCA.Web.Pages.Staff;

public sealed class EnquiriesModel(PublicData database) : PageModel
{
    private const int PageSize = 30;
    public List<Enquiry> Items { get; private set; } = [];
    public List<string> Categories { get; private set; } = [];
    public Enquiry? Selected { get; private set; }
    public string? Search { get; private set; }
    public string? Category { get; private set; }
    public int PageNumber { get; private set; }
    public bool HasNext { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid? id, string? search, string? category, int pageNumber,
        CancellationToken cancellationToken)
    {
        var enquiries = database.Enquiries.AsNoTracking();
        if (id.HasValue)
        {
            Selected = await enquiries.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            return Selected is null ? NotFound() : Page();
        }

        Categories = await enquiries.Select(item => item.Category).Distinct().OrderBy(value => value)
            .ToListAsync(cancellationToken);
        Search = search?.Trim();
        if (Search is { Length: > 120 })
            return BadRequest();
        Category = category;
        PageNumber = Math.Clamp(pageNumber, 1, 100_000);
        if (!string.IsNullOrWhiteSpace(Category))
            enquiries = enquiries.Where(item => item.Category == Category);
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var term = Search.ToUpperInvariant();
            enquiries = enquiries.Where(item => item.Email.ToUpper().Contains(term) ||
                item.Name.ToUpper().Contains(term) || item.Message.ToUpper().Contains(term) ||
                (item.Phone != null && item.Phone.Contains(term)));
        }

        Items = await enquiries.OrderByDescending(item => item.ReceivedAt).ThenBy(item => item.Id)
            .Skip((PageNumber - 1) * PageSize).Take(PageSize + 1).ToListAsync(cancellationToken);
        HasNext = Items.Count > PageSize;
        if (HasNext)
            Items.RemoveAt(PageSize);
        return Page();
    }
}
