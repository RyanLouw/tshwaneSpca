using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using TshwaneSPCA.Data;
using TshwaneSPCA.Web.PublicServices;
using TshwaneSPCA.Web.Staff;

namespace TshwaneSPCA.Web.Pages.Staff;

[AllowAnonymous]
[EnableRateLimiting("public-form")]
[RequestSizeLimit(16_384)]
public sealed class AuthModel(SignInManager<IdentityUser> signIn, UserManager<IdentityUser> users,
    StaffAccounts accounts, PublicData database) : PageModel
{
    [BindProperty] public Credentials Form { get; set; } = new();
    public string Operation { get; private set; } = "";
    public bool ResetRequested { get; private set; }

    public void OnGet([FromRoute] string operation, string? id, string? token)
    {
        Operation = operation;
        Form.Id = id;
        Form.Token = token;
    }

    public async Task<IActionResult> OnPostAsync([FromRoute] string operation, CancellationToken cancellationToken)
    {
        Operation = operation;
        if (operation == "set-password")
            return await SetPasswordAsync(cancellationToken);

        var email = Form.Email?.Trim();
        if (!ModelState.IsValid || !MailSettings.IsEmail(email))
        {
            ModelState.AddModelError(string.Empty, "Enter a valid email address.");
            return Page();
        }

        if (operation == "forgot-password")
        {
            await accounts.RequestResetAsync(email!, cancellationToken);
            ResetRequested = true;
            return Page();
        }

        if (!string.IsNullOrEmpty(Form.Password) &&
            (await signIn.PasswordSignInAsync(email!, Form.Password, isPersistent: false, lockoutOnFailure: true)).Succeeded)
            return RedirectToPage("/Staff/Index");

        ModelState.AddModelError(string.Empty, "Sign-in failed. Check your details or request a password reset.");
        return Page();
    }

    private async Task<IActionResult> SetPasswordAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || string.IsNullOrEmpty(Form.Password) || Form.Password != Form.ConfirmPassword)
        {
            ModelState.AddModelError(string.Empty, "Enter matching passwords of at least 12 characters.");
            return Page();
        }

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Form.Token ?? ""));
        }
        catch (FormatException)
        {
            token = "";
        }

        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(Form.Id) ||
            await users.FindByIdAsync(Form.Id) is not { } user)
        {
            ModelState.AddModelError(string.Empty, "This link is invalid or has expired.");
            return Page();
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var changed = await users.ResetPasswordAsync(user, token, Form.Password);
        if (changed.Succeeded)
        {
            user.EmailConfirmed = true;
            user.LockoutEnd = null;
            user.AccessFailedCount = 0;
            changed = await users.UpdateAsync(user);
        }

        if (changed.Succeeded)
        {
            await transaction.CommitAsync(cancellationToken);
            return RedirectToPage("/Staff/Auth", new { operation = "login" });
        }

        foreach (var error in changed.Errors)
            ModelState.AddModelError(string.Empty, error.Description);
        return Page();
    }

    public sealed class Credentials
    {
        [StringLength(254)] public string? Email { get; set; }
        [StringLength(128)] public string? Password { get; set; }
        [StringLength(128)] public string? ConfirmPassword { get; set; }
        [StringLength(64)] public string? Id { get; set; }
        [StringLength(2048)] public string? Token { get; set; }
    }
}
