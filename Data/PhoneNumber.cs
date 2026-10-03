namespace TSC.Data;

// A guardian's mobile number is the password to their child's portal, so the form it is
// stored in and the form it is typed in at sign-in have to agree exactly. Everything is
// reduced to the 11-digit national form: 01XXXXXXXXX.
public static class PhoneNumber
{
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var digits = new string(raw.Where(char.IsDigit).ToArray());

        // +8801712345678 and 8801712345678 both carry the country code.
        if (digits.Length == 13 && digits.StartsWith("880"))
            digits = "0" + digits[3..];

        // Bangladeshi mobiles are 11 digits and always start 01.
        return digits.Length == 11 && digits.StartsWith("01") ? digits : null;
    }
}
