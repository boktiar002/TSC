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

        // Anything already in keyword form goes through untouched.
        const string keywords = "Host=localhost;Database=tsc;Username=postgres;Password=x";
        Check(ConnectionString.Normalize(keywords) == keywords);

        Console.WriteLine("connection string selftest: all checks passed");
    }

    private static void Check(bool condition, [CallerArgumentExpression(nameof(condition))] string? expression = null)
    {
        if (!condition)
            throw new Exception($"connection string selftest FAILED: {expression}");
    }
}
