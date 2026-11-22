using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TshwaneSPCA.Web.Pages.Staff;

public sealed class IndexModel(SignInManager<IdentityUser> signIn) : PageModel
{
    public async Task<IActionResult> OnPostSignOutAsync()
    {
        await signIn.SignOutAsync();
        return RedirectToPage("/Staff/Auth", new { operation = "login" });
    }
}
