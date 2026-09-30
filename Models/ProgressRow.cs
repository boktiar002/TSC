namespace TSC.Models;

// One line of the progress sheet. Scores is one cell per selected paper, null where the
// student did not sit it.
public class ProgressRow
{
    public Student Student { get; init; } = null!;
    public decimal?[] Scores { get; init; } = [];
    public decimal Total { get; init; }
    public int PapersSat { get; init; }
    public decimal AveragePercent { get; init; }
    public int Rank { get; set; }
}
