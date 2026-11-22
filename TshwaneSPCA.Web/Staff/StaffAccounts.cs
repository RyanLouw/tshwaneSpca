using System.Data;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using TshwaneSPCA.Data;
using TshwaneSPCA.Web.PublicServices;

namespace TshwaneSPCA.Web.Staff;

public sealed class StaffAccounts(PublicData database, UserManager<IdentityUser> users, MailSettings mail, OutboxPayload outbox)
{
    public async Task<string?> InviteAsync(string email, bool firstAccount, CancellationToken cancellationToken)
    {
        if (!mail.Enabled)
            return "Configure the website email service before inviting staff.";
        if (!MailSettings.IsEmail(email))
            return "Enter a valid staff email address.";

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var count = await users.Users.CountAsync(cancellationToken);
        if (firstAccount && count != 0)
            return "The first staff account has already been created. Use the portal to invite other staff.";
        if (count >= 5)
            return "The portal supports up to five staff accounts.";
        if (await users.FindByEmailAsync(email) is not null)
            return "This email address already has a staff account.";

        var user = new IdentityUser { UserName = email, Email = email };
        var result = await users.CreateAsync(user);
        if (!result.Succeeded)
            return string.Join(" ", result.Errors.Select(error => error.Description));

        await QueuePasswordLinkAsync(user, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return null;
    }

    public async Task RequestResetAsync(string email, CancellationToken cancellationToken)
    {
        if (mail.Enabled && await users.FindByEmailAsync(email) is { } user)
            await QueuePasswordLinkAsync(user, cancellationToken);
    }

    private async Task QueuePasswordLinkAsync(IdentityUser user, CancellationToken cancellationToken)
    {
        var reset = await users.GeneratePasswordResetTokenAsync(user);
        var token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(reset));
        var url = QueryHelpers.AddQueryString($"{mail.PublicUrl.TrimEnd('/')}/staff/set-password",
            new Dictionary<string, string?> { ["id"] = user.Id, ["token"] = token });
        var subject = user.EmailConfirmed ? "Reset your Tshwane SPCA staff password" : "Your Tshwane SPCA staff invitation";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        database.Outbox.Add(outbox.Create(new EmailMessage(user.Email!, subject,
            $"Use this link within 24 hours to set your staff password:\n{url}\n\nIf you weren't expecting this email, ignore it."),
            now, now + 24 * 60 * 60));
        await database.SaveChangesAsync(cancellationToken);
    }
}
