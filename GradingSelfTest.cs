using System.Runtime.CompilerServices;
using TSC.Models;

// Run with: dotnet run -- selftest
internal static class GradingSelfTest
{
    public static void Run()
    {
        // Band boundaries are inclusive at the bottom of each band.
        Check(Grading.For(100).Letter == "A+");
        Check(Grading.For(80).Letter == "A+");
        Check(Grading.For(79.99m).Letter == "A");
        Check(Grading.For(70).Letter == "A");
        Check(Grading.For(69.99m).Letter == "A-");
        Check(Grading.For(60).Letter == "A-");
        Check(Grading.For(50).Letter == "B");
        Check(Grading.For(40).Letter == "C");
        Check(Grading.For(33).Letter == "D");
        Check(Grading.For(32.99m).Letter == "F");
        Check(Grading.For(0).Letter == "F");

        Check(Grading.For(80).Point == 5.00m);
        Check(Grading.For(0).Point == 0.00m);

        // Percentage rounds to 2dp and never divides by zero.
        Check(Grading.Percentage(50, 100) == 50m);
        Check(Grading.Percentage(1, 3) == 33.33m);
        Check(Grading.Percentage(0, 0) == 0m);
        Check(Grading.Percentage(10, 0) == 0m);
        Check(Grading.Percentage(150, 200) == 75m);

        // The fail threshold the result sheet uses per paper.
        Check(Grading.Percentage(32, 100) < 33);
        Check(Grading.Percentage(33, 100) >= 33);
        Check(Grading.Percentage(16, 50) < 33);   // 32% of a 50-mark paper
        Check(Grading.Percentage(17, 50) >= 33);  // 34%

        Check(Grading.Colour("A+") == "success");
        Check(Grading.Colour("F") == "danger");

        Console.WriteLine("grading selftest: all checks passed");
    }

    private static void Check(bool condition, [CallerArgumentExpression(nameof(condition))] string? expression = null)
    {
        if (!condition)
            throw new Exception($"grading selftest FAILED: {expression}");
    }
}
