using System.Runtime.CompilerServices;
using TSC.Data;

// Run with: dotnet run -- selftest
internal static class PhoneSelfTest
{
    public static void Run()
    {
        // The canonical form: 11 digits starting 01.
        Check(PhoneNumber.Normalize("01712345678") == "01712345678");

        // Country code, with and without the plus.
        Check(PhoneNumber.Normalize("+8801712345678") == "01712345678");
        Check(PhoneNumber.Normalize("8801712345678") == "01712345678");
        Check(PhoneNumber.Normalize("+880 1712 345678") == "01712345678");

        // Punctuation and spacing the office actually types.
        Check(PhoneNumber.Normalize("01712-345678") == "01712345678");
        Check(PhoneNumber.Normalize("01712 345678") == "01712345678");
        Check(PhoneNumber.Normalize("  01712345678  ") == "01712345678");

        // Every Bangladeshi mobile operator prefix.
        Check(PhoneNumber.Normalize("01312345678") == "01312345678");
        Check(PhoneNumber.Normalize("01912345678") == "01912345678");

        // Not a mobile number: return null rather than a partial string, so the caller
        // refuses to create a login instead of creating one nobody can use.
        Check(PhoneNumber.Normalize(null) == null);
        Check(PhoneNumber.Normalize("") == null);
        Check(PhoneNumber.Normalize("   ") == null);
        Check(PhoneNumber.Normalize("0171234567") == null);      // 10 digits
        Check(PhoneNumber.Normalize("017123456789") == null);    // 12 digits
        Check(PhoneNumber.Normalize("02123456789") == null);     // landline, not 01
        Check(PhoneNumber.Normalize("hello") == null);
        Check(PhoneNumber.Normalize("+8802123456789") == null);

        Console.WriteLine("phone selftest: all checks passed");
    }

    private static void Check(bool condition, [CallerArgumentExpression(nameof(condition))] string? expression = null)
    {
        if (!condition)
            throw new Exception($"phone selftest FAILED: {expression}");
    }
}
