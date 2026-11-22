using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TshwaneSPCA.Data;
using TshwaneSPCA.Web.Staff;

namespace TshwaneSPCA.Web.Pages.Staff;

[RequestSizeLimit(32_768)]
[RequestFormLimits(ValueLengthLimit = 4096, ValueCountLimit = 32)]
[Authorize(Policy = StaffAccess.Content)]
public sealed class ContentModel(PublicData database, SiteCopy copy) : PageModel
{
    public bool CanEditBanking => User.IsInRole(StaffAccess.Administrator);
    public IEnumerable<SiteField> Fields => SiteCopy.Fields.Where(item => CanEditBanking || !IsBanking(item.Key));

    private static bool IsBanking(string key) => key.StartsWith("Bank", StringComparison.OrdinalIgnoreCase);
    [BindProperty] public Dictionary<string, string> Values { get; set; } = [];
    [TempData] public bool Saved { get; set; }

    public async Task OnGetAsync()
    {
        foreach (var field in Fields)
            Values[field.Key] = await copy.ReadAsync(field.Key);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!CanEditBanking && Values.Keys.Any(IsBanking))
            return Forbid();

        foreach (var field in Fields)
        {
            var value = Values.GetValueOrDefault(field.Key)?.Trim();
            if (string.IsNullOrWhiteSpace(value) || value.Length > field.MaxLength)
                ModelState.AddModelError($"Values[{field.Key}]", $"{field.Label} is required and must be at most {field.MaxLength} characters.");
        }

        if (Values.GetValueOrDefault("BankAccountNumber") is { } account &&
            (!account.All(character => char.IsAsciiDigit(character) || character == ' ') ||
             account.Count(char.IsAsciiDigit) is < 6 or > 20))
            ModelState.AddModelError("Values[BankAccountNumber]", "Use 6–20 digits for the account number. Spaces are allowed.");

        if (Values.GetValueOrDefault("BankBranchCode") is { } branch &&
            (branch.Length != 6 || !branch.All(char.IsAsciiDigit)))
            ModelState.AddModelError("Values[BankBranchCode]", "Enter a six-digit branch code.");

        if (!ModelState.IsValid)
            return Page();

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var entries = await database.Content.ToDictionaryAsync(row => row.Key, cancellationToken);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var field in Fields)
        {
            var value = Values[field.Key].Trim();
            if (entries.TryGetValue(field.Key, out var entry))
            {
                if (entry.Value != value)
                {
                    entry.Value = value;
                    entry.UpdatedAt = now;
                }
            }
            else if (value != field.Default)
            {
                database.Content.Add(new SiteContent { Key = field.Key, Value = value, UpdatedAt = now });
            }
        }

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        Saved = true;
        return RedirectToPage();
    }
}
