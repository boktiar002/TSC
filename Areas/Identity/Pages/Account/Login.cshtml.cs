using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TSC.Models;

namespace TSC.Areas.Identity.Pages.Account;

// Identity's default UI signs in by email address. Students and teachers here sign in with the
// ID the centre already gave them -- see LoginProvisioning -- and most of them have no email at
// all, so this app-local page overrides the default one and asks for a login ID instead.
//
// Admins still have an email as their user name, and typing it still works: the lookup tries the
// user name first and falls back to email.
[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly UserManager<ApplicationUser> _users;

    public LoginModel(SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users)
    {
        _signIn = signIn;
        _users = users;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Enter your login ID.")]
        [Display(Name = "Login ID")]
        public string LoginId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter your password.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Keep me signed in")]
        public bool RememberMe { get; set; }
    }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
            ModelState.AddModelError(string.Empty, ErrorMessage);

        ReturnUrl = returnUrl;

        // A stale half-signed-in cookie makes the next sign-in fail for no visible reason.
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;

        if (!ModelState.IsValid)
            return Page();

        var typed = Input.LoginId.Trim();

        // Look up the account first so the sign-in call gets the real user name. Going straight
        // to PasswordSignInAsync(typed, ...) would reject an admin who typed their email.
        var user = await _users.FindByNameAsync(typed) ?? await _users.FindByEmailAsync(typed);

        if (user?.UserName == null)
        {
            // Deliberately the same message as a wrong password: saying "no such ID" would let
            // anyone test which student IDs exist.
            ModelState.AddModelError(string.Empty, "That login ID and password do not match.");
            return Page();
        }

        var result = await _signIn.PasswordSignInAsync(
            user.UserName, Input.Password, Input.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
            return LocalRedirect(returnUrl ?? Url.Content("~/"));

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty,
                "Too many failed attempts. Try again in a few minutes, or ask the office to reset your password.");

            return Page();
        }

        ModelState.AddModelError(string.Empty, "That login ID and password do not match.");

        return Page();
    }
}
