using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using TSC.Models;

namespace TSC.Data;

// Students and teachers get their login created for them by the admin, not by self-registration,
// so the centre stays in control of who has an account.
//
// The login ID is the person's Student ID or Teacher ID, never their email. Most students here
// have no email address at all, and an ID they already know — and that is already printed on
// their record — is one less thing for a guardian to remember at the counter. Email stays on the
// record as a contact detail; it is not what they sign in with.
public static class LoginProvisioning
{
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";   // no l
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";    // no I, O
    private const string Digits = "23456789";                   // no 0, 1
    private const string Symbols = "!@#$%*?";

    // Read aloud over a counter and typed on a phone, so ambiguous characters are left out.
    // One from each class first, to satisfy the Identity password rules by construction.
    public static string GeneratePassword()
    {
        var chars = new List<char>
        {
            Pick(Upper),
            Pick(Lower),
            Pick(Digits),
            Pick(Symbols),
        };

        const string all = Lower + Upper + Digits;

        while (chars.Count < 10)
            chars.Add(Pick(all));

        // Shuffle so the character classes are not always in the same positions.
        for (var i = chars.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars.ToArray());
    }

    // Identity rejects a user name containing anything outside its allowed set, and the error it
    // gives back ("User name 'X' is invalid, can only contain letters or digits") reads like a bug
    // to whoever typed the ID. Catch it here instead, where we can say which field is wrong.
    public static string? ValidateLoginId(UserManager<ApplicationUser> users, string loginId)
    {
        if (string.IsNullOrWhiteSpace(loginId))
            return "This person needs an ID before a login can be created for them.";

        var allowed = users.Options.User.AllowedUserNameCharacters;

        if (!string.IsNullOrEmpty(allowed) && loginId.Any(c => !allowed.Contains(c)))
            return $"\"{loginId}\" cannot be used as a login ID. Use letters, digits, - . _ only.";

        return null;
    }

    public static async Task<(ApplicationUser? User, string? Password, string? Error)> CreateAsync(
        UserManager<ApplicationUser> users, string loginId, string fullName, string role,
        string? email = null)
    {
        loginId = loginId?.Trim() ?? "";

        var invalid = ValidateLoginId(users, loginId);

        if (invalid != null)
            return (null, null, invalid);

        if (await users.FindByNameAsync(loginId) != null)
            return (null, null, $"\"{loginId}\" already has a login.");

        var password = GeneratePassword();

        var user = new ApplicationUser
        {
            UserName = loginId,
            // Blank rather than empty-string, so Identity's unique-email index (if it is ever
            // switched on) does not see every email-less student as a duplicate of the others.
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            EmailConfirmed = true,
            FullName = fullName,
            IssuedPassword = password
        };

        var created = await users.CreateAsync(user, password);

        if (!created.Succeeded)
            return (null, null, string.Join(" ", created.Errors.Select(e => e.Description)));

        var inRole = await users.AddToRoleAsync(user, role);

        if (!inRole.Succeeded)
        {
            // Don't leave a roleless orphan behind that can log in but see nothing.
            await users.DeleteAsync(user);
            return (null, null, string.Join(" ", inRole.Errors.Select(e => e.Description)));
        }

        return (user, password, null);
    }

    // Re-issue rather than deleting the account and starting over, which would drop the person's
    // link to their marks, attendance and fees.
    public static async Task<(ApplicationUser? User, string? Password, string? Error)> ResetPasswordAsync(
        UserManager<ApplicationUser> users, string userId)
    {
        var user = await users.FindByIdAsync(userId);

        if (user == null)
            return (null, null, "That login no longer exists.");

        var password = GeneratePassword();
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var reset = await users.ResetPasswordAsync(user, token, password);

        if (!reset.Succeeded)
            return (null, null, string.Join(" ", reset.Errors.Select(e => e.Description)));

        // Kept in step with the hash, or the admin screen would show the old password back.
        user.IssuedPassword = password;
        await users.UpdateAsync(user);

        return (user, password, null);
    }

    private static char Pick(string set) => set[RandomNumberGenerator.GetInt32(set.Length)];
}
