using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using TSC.Models;

namespace TSC.Data;

// Students and teachers get their login created for them by the admin, not by self-registration,
// so the centre stays in control of who has an account.
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

    public static async Task<(ApplicationUser? User, string? Password, string? Error)> CreateAsync(
        UserManager<ApplicationUser> users, string email, string fullName, string role)
    {
        if (string.IsNullOrWhiteSpace(email))
            return (null, null, "An email address is needed to create a login.");

        if (await users.FindByEmailAsync(email) != null)
            return (null, null, $"{email} already has a login.");

        var password = GeneratePassword();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName
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

    // The password is shown once, so a lost or mis-typed one cannot be looked up. Re-issue
    // instead of deleting the account and starting over, which would drop the person's link
    // to their marks, attendance and fees.
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

        return (user, password, null);
    }

    private static char Pick(string set) => set[RandomNumberGenerator.GetInt32(set.Length)];
}
