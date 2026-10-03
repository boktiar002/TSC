namespace TSC.Models;

// One student's report card, gathered once and rendered twice: as the printable web sheet and
// as the downloadable PDF. Both renderings read the same numbers, so a guardian comparing the
// printout against the file cannot find them disagreeing.
public class ReportCardModel
{
    public required Student Student { get; init; }

    // Every exam this class has sat, which is what the "exams on this card" picker offers.
    public required List<Exam> Exams { get; init; }

    // The ones actually on the card.
    public required List<Exam> Chosen { get; init; }

    public required List<(Exam Exam, ExamSubject Paper)> Columns { get; init; }

    public required ProgressRow Row { get; init; }

    public int ClassSize { get; init; }
    public int AttendanceDays { get; init; }
    public int AttendancePresent { get; init; }
    public DateOnly ThisMonth { get; init; }
    public decimal MonthlyFee { get; init; }
    public decimal PaidThisMonth { get; init; }

    public decimal FullMarks => Columns.Sum(c => c.Paper.FullMarks);
    public decimal Due => Math.Max(0, MonthlyFee - PaidThisMonth);

    public decimal? AttendanceRate => AttendanceDays == 0
        ? null
        : Math.Round(AttendancePresent * 100m / AttendanceDays, 1);

    public (string Letter, decimal Point) Grade => Grading.For(Row.AveragePercent);

    public string Title => Chosen.Count == 1 ? Chosen[0].Name : "Progress report";

    // 0 means unranked: an archived student, kept out of the live merit list.
    public string Place => Row.Rank switch
    {
        0 => "—",
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        var r when r % 10 == 1 && r % 100 != 11 => $"{r}st",
        var r when r % 10 == 2 && r % 100 != 12 => $"{r}nd",
        var r when r % 10 == 3 && r % 100 != 13 => $"{r}rd",
        var r => $"{r}th",
    };

    // Per-paper line, worked out once so the view and the PDF cannot drift.
    public IEnumerable<PaperLine> Lines()
    {
        for (var i = 0; i < Columns.Count; i++)
        {
            var (exam, paper) = Columns[i];
            var score = Row.Scores[i];
            var percent = score == null ? 0 : Grading.Percentage(score.Value, paper.FullMarks);

            yield return new PaperLine(
                exam.Name,
                exam.ExamDate,
                paper.Subject?.Name ?? "—",
                paper.FullMarks,
                score,
                percent,
                Grading.For(percent).Letter);
        }
    }

    public record PaperLine(
        string ExamName,
        DateOnly ExamDate,
        string SubjectName,
        decimal FullMarks,
        decimal? Score,
        decimal Percent,
        string GradeLetter);
}
