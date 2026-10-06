using Npgsql;

namespace TSC.Data;

public static class ConnectionString
{
    // Render, Heroku, Neon and friends hand out the database as a URL. Npgsql only parses
    // `Key=Value;`, so a URL dies with "Format of the initialization string does not conform
    // to specification starting at index 0" before the app ever serves a request.
    // Anything that is not a URL is passed through untouched.
    public static string Normalize(string value)
    {
        if (!value.StartsWith("postgres://") && !value.StartsWith("postgresql://"))
            return value;

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);

        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port < 0 ? 5432 : uri.Port,
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
            Database = uri.AbsolutePath.TrimStart('/')
            // ponytail: SslMode left at Npgsql's default (Prefer) — it negotiates TLS with
            // Render and still works against a local server. Set it explicitly if a host
            // ever needs VerifyFull.
        }.ConnectionString;
    }
}
