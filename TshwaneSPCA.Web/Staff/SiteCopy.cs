using Microsoft.EntityFrameworkCore;
using TshwaneSPCA.Data;

namespace TshwaneSPCA.Web.Staff;

public sealed class SiteCopy(PublicData database)
{
    public static IReadOnlyList<SiteField> Fields { get; } =
    [
        new("HomeTitle", "Home heading", "Every animal deserves a second chance.", 130),
        new("HomeIntro", "Home introduction", "Protecting animals, bringing families together and giving our community meaningful ways to make a difference.", 500),
        new("AboutSubtitle", "About Us subtitle", "Over 116 Years of Compassion, Protection and Hope", 160),
        new("AboutIntro", "About Us opening paragraph", "For more than a century, the Tshwane SPCA has been a voice for the voiceless, a protector of the vulnerable and a source of hope for animals in need.", 700),
        new("VolunteerIntro", "Volunteering introduction", "Your time, energy and compassion can make a real difference. Ask the branch about current volunteer opportunities and any orientation or requirements.", 500),
        new("DonationTitle", "Donations heading", "Your kindness makes a difference.", 130),
        new("DonationIntro", "Donations introduction", "Help us care for animals with a financial contribution or a donation of useful supplies. Every thoughtful gift is appreciated.", 500),
        new("BankAccountName", "Bank account holder", "Tshwane SPCA", 100),
        new("BankName", "Bank and account type", "FNB · Cheque Account", 100),
        new("BankAccountNumber", "Bank account number", "561 6000 7452", 26),
        new("BankBranchCode", "Bank branch code", "261550", 6),
        new("BankDonationReference", "Donation reference", "Donation", 60),
        new("BankSponsorshipReference", "Sponsorship reference", "Kennels", 60)
    ];

    private Task<Dictionary<string, string>>? saved;

    public async Task<string> ReadAsync(string key)
    {
        var entries = await ReadSavedAsync();
        return entries.GetValueOrDefault(key) ?? Fields.Single(field => field.Key == key).Default;
    }

    public async Task<bool> ShowOriginalBankPostersAsync()
    {
        var entries = await ReadSavedAsync();
        return Fields.Where(field => field.Key.StartsWith("Bank", StringComparison.Ordinal))
            .All(field => !entries.TryGetValue(field.Key, out var value) || value == field.Default);
    }

    private Task<Dictionary<string, string>> ReadSavedAsync() =>
        saved ??= database.Content.AsNoTracking().ToDictionaryAsync(row => row.Key, row => row.Value);
}

public sealed record SiteField(string Key, string Label, string Default, int MaxLength);
