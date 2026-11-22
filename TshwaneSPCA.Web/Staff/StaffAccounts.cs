using System.Data;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using TshwaneSPCA.Data;
using TshwaneSPCA.Web.PublicServices;

namespace TshwaneSPCA.Web.Staff;

public sealed class StaffAccounts(PublicData database, UserManager<IdentityUser> users, RoleManager<IdentityRole> roles, MailSettings mail, OutboxPayload outbox)
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

        var error = await CreateInvitedUserAsync(email, firstAccount, cancellationToken);
        if (error is null)
            await transaction.CommitAsync(cancellationToken);
        return error;
    }

    public async Task<string?> ProvisionInitialAdminAsync(string email, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if ((await users.GetUsersInRoleAsync(StaffAccess.Administrator)).Count != 0)
            return null;

        if (!MailSettings.IsEmail(email))
            return "Staff__InitialAdminEmail must be a valid email address.";

        string? error;
        if (await users.Users.AnyAsync(cancellationToken))
        {
            var existing = await users.FindByEmailAsync(email);
            if (existing is null || !existing.EmailConfirmed)
                return "Staff__InitialAdminEmail must match an existing confirmed staff account on this installation.";

            var result = await AddRoleAsync(existing, StaffAccess.Administrator);
            error = result.Succeeded ? null : "Could not grant the initial administrator role.";
        }
        else
        {
            if (!mail.Enabled)
                return "Configure the email provider before enabling Staff__InitialAdminEmail.";

            error = await CreateInvitedUserAsync(email, administrator: true, cancellationToken);
            if (error is not null)
                return "Could not create the initial administrator invitation.";
        }

        if (error is null)
            await transaction.CommitAsync(cancellationToken);
        return error;
    }

    private async Task<string?> CreateInvitedUserAsync(string email, bool administrator, CancellationToken cancellationToken)
    {
        var user = new IdentityUser { UserName = email, Email = email };
        var result = await users.CreateAsync(user);
        if (!result.Succeeded)
            return string.Join(" ", result.Errors.Select(error => error.Description));

        if (administrator)
        {
            var roleResult = await AddRoleAsync(user, StaffAccess.Administrator);
            if (!roleResult.Succeeded)
                return Describe(roleResult);
        }

        await QueuePasswordLinkAsync(user, cancellationToken);
        return null;
    }

    public async Task<string?> PromoteFirstAdminAsync(string email, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if ((await users.GetUsersInRoleAsync(StaffAccess.Administrator)).Count != 0)
            return "An administrator already exists.";
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null || !user.EmailConfirmed)
            return "Choose an existing staff account that has already accepted its invitation.";

        var result = await AddRoleAsync(user, StaffAccess.Administrator);
        if (!result.Succeeded)
            return Describe(result);

        await transaction.CommitAsync(cancellationToken);
        return null;
    }

    public async Task<string?> SetModulesAsync(IdentityUser member, IReadOnlyCollection<string> modules,
        CancellationToken cancellationToken)
    {
        if (modules.Any(module => !StaffAccess.Modules.ContainsKey(module)))
            return "Choose valid staff permissions.";

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var current = await users.GetRolesAsync(member);
        if (current.Contains(StaffAccess.Administrator))
            return "Administrator permissions are fixed.";

        var unwanted = current.Where(role => StaffAccess.Modules.ContainsKey(role) && !modules.Contains(role)).ToArray();
        if (unwanted.Length != 0)
        {
            var removed = await users.RemoveFromRolesAsync(member, unwanted);
            if (!removed.Succeeded)
                return Describe(removed);
        }

        foreach (var module in modules.Distinct().Except(current))
        {
            var granted = await AddRoleAsync(member, module);
            if (!granted.Succeeded)
                return Describe(granted);
        }

        await transaction.CommitAsync(cancellationToken);
        return null;
    }

    private async Task<IdentityResult> AddRoleAsync(IdentityUser user, string role)
    {
        if (!await roles.RoleExistsAsync(role))
        {
            var created = await roles.CreateAsync(new IdentityRole(role));
            if (!created.Succeeded)
                return created;
        }

        return await users.AddToRoleAsync(user, role);
    }

    private static string Describe(IdentityResult result) =>
        string.Join(" ", result.Errors.Select(error => error.Description));

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
