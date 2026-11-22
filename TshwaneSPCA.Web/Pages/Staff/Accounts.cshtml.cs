using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TshwaneSPCA.Web.Staff;

namespace TshwaneSPCA.Web.Pages.Staff;

[EnableRateLimiting("public-form")]
public sealed class AccountsModel(UserManager<IdentityUser> users, StaffAccounts accounts) : PageModel
{
    public List<IdentityUser> Members { get; private set; } = [];
    [BindProperty, Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [TempData] public string? Status { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Members = await users.Users.AsNoTracking().OrderBy(user => user.Email).ToListAsync(cancellationToken);

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var error = await accounts.InviteAsync(Email.Trim(), firstAccount: false, cancellationToken);
            if (error is null)
            {
                Status = "The invitation email was queued.";
                return RedirectToPage();
            }
            ModelState.AddModelError(string.Empty, error);
        }

        await OnGetAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostRemoveAsync(string id, CancellationToken cancellationToken)
    {
        if (id == users.GetUserId(User))
            return BadRequest();
        var member = await users.FindByIdAsync(id);
        if (member is null)
            return NotFound();

        var removed = await users.DeleteAsync(member);
        if (removed.Succeeded)
        {
            Status = "Staff access removed.";
            return RedirectToPage();
        }

        foreach (var error in removed.Errors)
            ModelState.AddModelError(string.Empty, error.Description);
        await OnGetAsync(cancellationToken);
        return Page();
    }
}
