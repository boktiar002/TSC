using System.Runtime.CompilerServices;
using Npgsql;
using TSC.Data;

// Run with: dotnet run -- selftest
//
// A connection string the driver cannot parse only shows up as a crashed container on the
// host -- the slowest feedback loop there is. Check the parsing here instead.
internal static class ConnectionStringSelfTest
{
    public static void Run()
    {
        // The shape Render hands out, password carrying characters that must be url-encoded.
        var render = new NpgsqlConnectionStringBuilder(ConnectionString.Normalize(
            "postgresql://tsc_user:p%40ss%3Aword@dpg-abc.oregon-postgres.render.com:5432/tsc_db"));

        Check(render.Host == "dpg-abc.oregon-postgres.render.com");
        Check(render.Port == 5432);
        Check(render.Username == "tsc_user");
        Check(render.Password == "p@ss:word");
        Check(render.Database == "tsc_db");

        // No port in the URL means Postgres' default, not the -1 Uri reports.
        Check(new NpgsqlConnectionStringBuilder(
            ConnectionString.Normalize("postgres://u:p@host/db")).Port == 5432);

        // What a dashboard paste actually looks like: quotes off a shell example, a newline
        // off the end, a leading space. All of these reached Npgsql as-is and died at index 0.
        foreach (var messy in new[]
                 {
                     "\"postgres://u:p@host/db\"",
                     "'postgres://u:p@host/db'",
                     " postgres://u:p@host/db ",
                     "postgres://u:p@host/db\n",
                     "\"postgres://u:p@host/db\"\r\n",
                 })
        {
            var parsed = new NpgsqlConnectionStringBuilder(ConnectionString.Normalize(messy));
            Check(parsed.Host == "host");
            Check(parsed.Database == "db");
            Check(parsed.Password == "p");
        }

        // Keyword form survives the same treatment and is otherwise untouched.
        const string keywords = "Host=localhost;Database=tsc;Username=postgres;Password=x";
        Check(ConnectionString.Normalize(keywords) == keywords);
        Check(new NpgsqlConnectionStringBuilder(ConnectionString.Normalize($"  \"{keywords}\"\n")).Host == "localhost");

        // Hopeless input says so, instead of letting the driver's "index 0" through.
        var failure = Catch(() => ConnectionString.Normalize("psql -h host -U user db"));
        Check(failure is InvalidOperationException);
        Check(failure!.Message.Contains("psql -h host"));
        // The password is never in the message.
        Check(!Catch(() => ConnectionString.Normalize("nonsense://@@@ hunter2"))!.Message.Contains("hunter2"));

        Console.WriteLine("connection string selftest: all checks passed");
    }

    private static Exception? Catch(Action action)
    {
        try { action(); return null; }
        catch (Exception e) { return e; }
    }

    private static void Check(bool condition, [CallerArgumentExpression(nameof(condition))] string? expression = null)
    {
        if (!condition)
            throw new Exception($"connection string selftest FAILED: {expression}");
    }
}
