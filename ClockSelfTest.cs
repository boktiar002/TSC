using System.Runtime.CompilerServices;
using TSC.Data;

// Run with: dotnet run -- selftest
//
// Clock pins "today" to Dhaka. On a machine whose own zone is already +06 that is invisible --
// Clock.Today and DateTime.Today agree -- so check the conversion itself, not the rendered date.
internal static class ClockSelfTest
{
    public static void Run()
    {
        var utc = DateTime.UtcNow;

        // Bangladesh Standard Time is UTC+6 the whole year; there is no daylight saving to
        // account for. If this fails, the zone database on the host is not what we think.
        Check(Math.Abs((Clock.ToDhaka(utc) - utc).TotalHours - 6) < 0.001);

        Check(Clock.Today == DateOnly.FromDateTime(Clock.Now));

        // Fee months are always day 1 of the month.
        Check(Clock.ThisMonth.Day == 1);
        Check(Clock.ThisMonth.Month == Clock.Today.Month);
        Check(Clock.ThisMonth.Year == Clock.Today.Year);
        Check(Clock.FirstOf(new DateOnly(2026, 10, 3)) == new DateOnly(2026, 10, 1));
        Check(Clock.FirstOf(new DateOnly(2026, 10, 1)) == new DateOnly(2026, 10, 1));
        Check(Clock.FirstOf(new DateOnly(2026, 12, 31)) == new DateOnly(2026, 12, 1));

        // The bug this guards. A UTC-hosted server rolls over to a new date at 6 AM Dhaka --
        // exactly when the morning batch sits down. The 6:30 AM roll call must file under the
        // day the office is living in, not the one UTC has just left.
        var morningBatch = new DateTime(2026, 10, 3, 0, 30, 0, DateTimeKind.Utc); // 06:30 in Dhaka
        Check(DateOnly.FromDateTime(Clock.ToDhaka(morningBatch)) == new DateOnly(2026, 10, 3));

        // And the other edge: 11 PM Dhaka is still the same day, though UTC says 5 PM.
        var lateEvening = new DateTime(2026, 10, 3, 17, 0, 0, DateTimeKind.Utc); // 23:00 in Dhaka
        Check(DateOnly.FromDateTime(Clock.ToDhaka(lateEvening)) == new DateOnly(2026, 10, 3));

        // Just before midnight UTC is already tomorrow in Dhaka.
        var beforeUtcMidnight = new DateTime(2026, 10, 3, 23, 0, 0, DateTimeKind.Utc); // 05:00 on the 4th
        Check(DateOnly.FromDateTime(Clock.ToDhaka(beforeUtcMidnight)) == new DateOnly(2026, 10, 4));

        Console.WriteLine("clock selftest: all checks passed");
    }

    private static void Check(bool condition, [CallerArgumentExpression(nameof(condition))] string? expression = null)
    {
        if (!condition)
            throw new Exception($"clock selftest FAILED: {expression}");
    }
}
