using Microsoft.AspNetCore.Identity;

namespace TSC.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    // The password in the clear, for student and teacher portal accounts only.
    //
    // This is a deliberate, eyes-open trade. The centre hands credentials to guardians at a
    // counter, often months after the account was made, and a hashed-only password meant the
    // office had to reset it every time someone forgot — which locked the student out of the
    // account they were mid-way through using. The admin can read this back instead.
    //
    // Consequences, so nobody is surprised later: anyone with the admin role or database
    // access can read every student's password. Admin accounts themselves are never stored
    // here (see LoginProvisioning), so an admin password still cannot be read off the screen.
    // Students who change their own password via /Identity/Account/Manage clear this field,
    // and the admin sees "changed by the student" rather than a stale value.
    //
    // ponytail: plaintext by product decision, not oversight. If this ever needs to stop
    // being readable, delete this column and switch the admin screens to reset-and-show-once.
    [PersonalData]
    public string? IssuedPassword { get; set; }
}
