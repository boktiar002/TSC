using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TSC.Models;

namespace TSC.Areas.Identity.Pages.Account.Manage;

// Overrides the default UI page for one reason: the admin screens show a student's password
// back from ApplicationUser.IssuedPassword, and a student who changes their own password here
// would leave that copy stale. A stale copy is worse than none -- the office would read out a
// password that no longer works. So changing it clears the stored one, and the admin screens
// say "changed by the student" instead.
public class ChangePasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;

    public ChangePasswordModel(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn)
    {
        _users = users;
        _signIn = signIn;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Current password")]
        public string OldPassword { get; set; } = string.Empty;

        [Required]
        [StringLength(100, ErrorMessage = "The password must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm new password")]
        [Compare(nameof(NewPassword), ErrorMessage = "The two passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _users.GetUserAsync(User);

        return user == null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _users.GetUserAsync(User);

        if (user == null)
            return NotFound();

        if (!ModelState.IsValid)
            return Page();

        var changed = await _users.ChangePasswordAsync(user, Input.OldPassword, Input.NewPassword);

        if (!changed.Succeeded)
        {
            foreach (var error in changed.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return Page();
        }

        // The admin's readable copy is no longer the real password.
        if (user.IssuedPassword != null)
        {
            user.IssuedPassword = null;
            await _users.UpdateAsync(user);
        }

        await _signIn.RefreshSignInAsync(user);

        StatusMessage = "Your password has been changed.";

        return RedirectToPage();
    }
}
