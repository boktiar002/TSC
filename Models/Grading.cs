namespace TSC.Models;

// ponytail: hard-coded Bangladeshi GPA-5 scale, the one thing to edit if this centre grades
// differently. Move to a settings table only when a second scale actually shows up.
public static class Grading
{
    private static readonly (decimal Min, string Letter, decimal Point)[] Scale =
    {
        (80, "A+", 5.00m),
        (70, "A",  4.00m),
        (60, "A-", 3.50m),
        (50, "B",  3.00m),
        (40, "C",  2.00m),
        (33, "D",  1.00m),
        (0,  "F",  0.00m),
    };

    public static (string Letter, decimal Point) For(decimal percentage)
    {
        var band = Scale.First(b => percentage >= b.Min);

        return (band.Letter, band.Point);
    }

    public static decimal Percentage(decimal obtained, decimal outOf) =>
        outOf <= 0 ? 0 : Math.Round(obtained * 100 / outOf, 2);

    // Bootstrap colour for the badge, so views don't each invent their own mapping.
    public static string Colour(string letter) => letter switch
    {
        "A+" or "A" => "success",
        "A-" or "B" => "primary",
        "C" or "D" => "warning",
        _ => "danger",
    };
}
