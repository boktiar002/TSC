using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TSC.Areas.Identity.Pages.Account;

// Closed along with Register: the default page takes an email address from the query string and
// looks the account up, which tells an anonymous caller whether that address is registered.
public class RegisterConfirmationModel : PageModel
{
    public IActionResult OnGet() => NotFound();
}
