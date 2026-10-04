using Microsoft.AspNetCore.Identity;

namespace TSC.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    // The password as it was handed to the student or teacher, kept so the office can read it
    // back off the Logins screen when someone forgets it. Cleared the moment its owner changes
    // their own password: a stale copy is worse than none, because the office would read out a
    // password that no longer works.
    public string? IssuedPassword { get; set; }
}
