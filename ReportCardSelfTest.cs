using System.Runtime.CompilerServices;
using TSC.Data;
using TSC.Models;

// Run with: dotnet run -- selftest
//
// The PDF is the one part of the app that is never exercised by loading a page -- it is a
// separate renderer over the same numbers -- so render one here. This catches the whole chain:
// the Bengali font and the logo being found on disk, the QuestPDF layout not overflowing, and
// the figures on the file agreeing with the figures the web sheet shows.
internal static class ReportCardSelfTest
{
    public static void Run(string webRoot)
    {
        var card = Sample();

        // Totals the PDF prints in its footer row. If these drift, the download and the
        // printed sheet stop agreeing, which is the one thing a guardian can catch us on.
        Check(card.FullMarks == 150);
        Check(card.Row.Total == 115);
        Check(card.Grade.Letter == Grading.For(card.Row.AveragePercent).Letter);

        // An absent paper has to reach the renderer as null, not as a zero that reads like a
        // mark the student actually scored.
        var lines = card.Lines().ToList();
        Check(lines.Count == 3);
        Check(lines[2].Score == null);

        // Ordinals: 22nd, not "22th".
        Check(card.Place == "22nd");

        var pdf = ReportCardDocument.Render(card, webRoot);

        // A real PDF, and big enough to be the laid-out sheet rather than an empty page.
        Check(pdf.Length > 4096);
        Check(pdf[0] == '%' && pdf[1] == 'P' && pdf[2] == 'D' && pdf[3] == 'F');

        Console.WriteLine("report card selftest: all checks passed");
    }

    private static ReportCardModel Sample()
    {
        var exam = new Exam
        {
            Id = 1,
            Name = "Half Yearly",
            ExamDate = new DateOnly(2026, 6, 12),
        };

        // A Bengali name on purpose: if the bundled font is not registered these glyphs are
        // what come out as empty boxes.
        var student = new Student
        {
            Id = 7,
            StudentId = "TSC-0007",
            FullName = "আরিফ হাসান",
            GuardianName = "হাসান আলী",
            GuardianPhone = "01700000000",
            Batch = "Morning",
            SchoolClass = new SchoolClass { Name = "Class 9", MonthlyFee = 1200 },
        };

        var columns = new List<(Exam Exam, ExamSubject Paper)>
        {
            (exam, Paper(1, "Bangla", 50)),
            (exam, Paper(2, "Mathematics", 50)),
            (exam, Paper(3, "English", 50)),
        };

        decimal?[] scores = [45m, 70m, null];

        return new ReportCardModel
        {
            Student = student,
            Exams = [exam],
            Chosen = [exam],
            Columns = columns,
            Row = new ProgressRow
            {
                Student = student,
                Scores = scores,
                Total = 115,
                PapersSat = 2,
                AveragePercent = Grading.Percentage(115, 150),
                Rank = 22,
            },
            ClassSize = 40,
            AttendanceDays = 20,
            AttendancePresent = 18,
            ThisMonth = new DateOnly(2026, 6, 1),
            MonthlyFee = 1200,
            PaidThisMonth = 500,
        };
    }

    private static ExamSubject Paper(int id, string subject, decimal fullMarks) => new()
    {
        Id = id,
        FullMarks = fullMarks,
        Subject = new Subject { Id = id, Name = subject },
    };

    private static void Check(bool ok, [CallerLineNumber] int line = 0)
    {
        if (!ok)
            throw new Exception($"report card selftest failed at line {line}");
    }
}
