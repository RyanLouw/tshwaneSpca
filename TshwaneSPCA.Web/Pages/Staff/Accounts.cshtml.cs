using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TshwaneSPCA.Web.Staff;

namespace TshwaneSPCA.Web.Pages.Staff;

[EnableRateLimiting("public-form")]
[Authorize(Roles = StaffAccess.Administrator)]
public sealed class AccountsModel(UserManager<IdentityUser> users, StaffAccounts accounts) : PageModel
{
    public List<IdentityUser> Members { get; private set; } = [];
    public Dictionary<string, IList<string>> Access { get; } = [];
    [BindProperty, Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [TempData] public string? Status { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Members = await users.Users.AsNoTracking().OrderBy(user => user.Email).ToListAsync(cancellationToken);
        foreach (var member in Members)
            Access[member.Id] = await users.GetRolesAsync(member);
    }

    public async Task<IActionResult> OnPostPermissionsAsync(string id, string[]? modules, CancellationToken cancellationToken)
    {
        var member = await users.FindByIdAsync(id);
        if (member is null)
            return NotFound();

        var error = await accounts.SetModulesAsync(member, modules ?? [], cancellationToken);
        if (error is null)
        {
            Status = "Staff permissions saved.";
            return RedirectToPage();
        }

        ModelState.AddModelError(string.Empty, error);
        await OnGetAsync(cancellationToken);
        return Page();
    }

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

        if (await users.IsInRoleAsync(member, StaffAccess.Administrator))
            return Forbid();

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
