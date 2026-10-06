using Npgsql;

namespace TSC.Data;

public static class ConnectionString
{
    // The connection string is typed into a hosting dashboard by hand, so it arrives with
    // whatever came along for the ride: a trailing newline, the quotes off a shell example,
    // a leading space. Render, Heroku and Neon also hand the database out as a URL, which
    // Npgsql cannot parse at all. Both end as the same opaque driver error --
    // "Format of the initialization string does not conform to specification starting at
    // index 0" -- so clean the value up here and say something useful when it is hopeless.
    public static string Normalize(string value)
    {
        var cleaned = value.Trim().Trim('"', '\'').Trim();

        try
        {
            return cleaned.Contains("://") ? FromUrl(cleaned) : new NpgsqlConnectionStringBuilder(cleaned).ConnectionString;
        }
        catch (Exception e) when (e is ArgumentException or UriFormatException or IndexOutOfRangeException)
        {
            throw new InvalidOperationException(
                $"The connection string is neither a 'Key=Value;' string nor a postgres:// URL. " +
                $"It is {value.Length} characters and starts with: {Preview(value)}  " +
                $"Check for quotes or a line break in the environment variable.", e);
        }
    }

    private static string FromUrl(string url)
    {
        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':', 2);

        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port < 0 ? 5432 : uri.Port,
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
            Database = uri.AbsolutePath.TrimStart('/')
            // ponytail: SslMode left at Npgsql's default (Prefer) -- it negotiates TLS with
            // Render and still works against a local server. Set it explicitly if a host
            // ever needs VerifyFull.
        }.ConnectionString;
    }

    // Enough of the value to recognise a stray quote, not enough to leak the password.
    private static string Preview(string value) =>
        string.Concat(value.Take(12).Select(c => char.IsControl(c) ? '\u00b7' : c));
}
