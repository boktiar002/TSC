using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TSC.Areas.Identity.Pages.Account;

// Logins are created for students and teachers by the admin -- see LoginProvisioning -- never by
// self-registration, so the centre stays in control of who has an account. Identity's default UI
// ships a public /Identity/Account/Register page regardless; this app-local page overrides it and
// closes the door. Deleting this file re-opens public sign-up.
public class RegisterModel : PageModel
{
    public IActionResult OnGet() => NotFound();

    public IActionResult OnPost() => NotFound();
}
