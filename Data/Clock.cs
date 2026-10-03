namespace TSC.Data;

// The centre is in Bangladesh; the server it runs on may not be. "Today" has to mean today in
// Dhaka, or a 6 AM roll call on a UTC-hosted server files itself under yesterday -- UTC midnight
// is 6 AM here, exactly when the morning batch sits down.
//
// Dates the office types (attendance, fee months, exam dates) are DateOnly with no zone, so
// they need no conversion. This is only about what the app assumes when nobody typed a date.
public static class Clock
{
    // IANA id: .NET maps it to the Windows zone automatically, so this is the same zone on a
    // dev laptop and on a Linux server.
    private static readonly TimeZoneInfo Dhaka = TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka");

    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Dhaka);

    public static DateOnly Today => DateOnly.FromDateTime(Now);

    // Fee months are always stored on day 1.
    public static DateOnly ThisMonth => FirstOf(Today);

    public static DateOnly FirstOf(DateOnly date) => new(date.Year, date.Month, 1);

    // For timestamps stored in UTC (notices, voided receipts): what the office saw on its clock.
    public static DateTime ToDhaka(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, Dhaka);
}
