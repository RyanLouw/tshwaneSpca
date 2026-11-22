using Microsoft.AspNetCore.Authorization;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TshwaneSPCA.Data;

using TshwaneSPCA.Web.Staff;

namespace TshwaneSPCA.Web.Pages.Staff;

[Authorize(Policy = StaffAccess.Subscribers)]
public sealed class SubscribersModel(PublicData database) : PageModel
{
    private const int PageSize = 30;
    public List<Subscriber> Items { get; private set; } = [];
    public int PageNumber { get; private set; }
    public bool HasNext { get; private set; }

    public async Task OnGetAsync(int pageNumber, CancellationToken cancellationToken)
    {
        PageNumber = Math.Clamp(pageNumber, 1, 100_000);
        Items = await database.Subscribers.AsNoTracking().OrderByDescending(item => item.ConsentAt).ThenBy(item => item.Id)
            .Skip((PageNumber - 1) * PageSize).Take(PageSize + 1).ToListAsync(cancellationToken);
        HasNext = Items.Count > PageSize;
        if (HasNext)
            Items.RemoveAt(PageSize);
    }

    public async Task<IActionResult> OnGetExportAsync(CancellationToken cancellationToken)
    {
        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers.ContentDisposition = "attachment; filename=newsletter-subscribers.csv";
        await Response.WriteAsync("email,consent_version,consented_at_utc,confirmed_at_utc\r\n", cancellationToken);
        var active = database.Subscribers.AsNoTracking()
            .Where(row => row.ConfirmedAt != null && row.UnsubscribedAt == null)
            .OrderBy(row => row.NormalizedEmail);
        await foreach (var row in active.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            await Response.WriteAsync($"{Cell(row.Email)},{Cell(row.ConsentVersion)}," +
                $"{UtcDate(row.ConsentAt)},{UtcDate(row.ConfirmedAt!.Value)}\r\n", cancellationToken);
        }
        return new EmptyResult();
    }

    private static string UtcDate(long timestamp) =>
        DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static string Cell(string value)
    {
        var first = value.TrimStart().FirstOrDefault();
        if (first is '=' or '+' or '-' or '@' || value.StartsWith('\t') || value.StartsWith('\r') || value.StartsWith('\n'))
            value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
